#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
fnv_to_fo4_converter.py — Fallout: New Vegas to Fallout 4 Asset Converter CLI
Automates headless NIF mesh conversion and material descriptor generation (.BGSM/.BGEM)
using the verified NifSkope conversion engine from FNV to FO4 (Nexus mod 75425).

Designed for batch and single-file processing in the FalloutRemake asset pipeline.
"""

import os
import sys
import json
import time
import shutil
import struct
import argparse
import subprocess
from pathlib import Path
from typing import List, Dict, Any, Optional

DEFAULT_NIFSKOPE_EXE = Path(
    r"D:\Downloads\Fallout Tools\fnv-to-fo4-v63-75425-v63-1718904136\build\nifskope_converter\release\NifSkope.exe"
)

def inspect_nif_header(nif_path: Path) -> Dict[str, Any]:
    """Inspects NIF header to verify version and format."""
    if not nif_path.exists():
        return {"exists": False}
    size = nif_path.stat().st_size
    with open(nif_path, "rb") as f:
        header = f.read(128)
    newline_pos = header.find(b"\n")
    if newline_pos == -1:
        return {"exists": True, "size": size, "valid": False, "error": "No newline in header"}
    magic = header[:newline_pos].decode("ascii", errors="replace").strip()
    offset = newline_pos + 1
    if len(header) < offset + 18:
        return {"exists": True, "size": size, "magic": magic, "valid": False}
    ver, endian, user_ver, num_blocks, user_ver2 = struct.unpack("<IBIII", header[offset:offset+17])
    
    is_fnv = (user_ver == 11 and user_ver2 == 34)
    is_fo4 = (user_ver == 12 and user_ver2 == 130)
    
    return {
        "exists": True,
        "size": size,
        "magic": magic,
        "version": hex(ver),
        "endian": endian,
        "user_ver": user_ver,
        "num_blocks": num_blocks,
        "user_ver2": user_ver2,
        "is_fnv": is_fnv,
        "is_fo4": is_fo4,
        "valid": True
    }

def process_bgsm_shadows(materials_dir: Path) -> int:
    """Applies bCastShadows: true to all .BGSM JSON files in the materials directory."""
    if not materials_dir.exists():
        return 0
    updated_count = 0
    for root, _, files in os.walk(materials_dir):
        for f in files:
            if f.lower().endswith(".bgsm"):
                bgsm_path = Path(root) / f
                try:
                    with open(bgsm_path, "r", encoding="utf-8") as fp:
                        data = json.load(fp)
                    if isinstance(data, dict):
                        data["bCastShadows"] = True
                        with open(bgsm_path, "w", encoding="utf-8") as fp:
                            json.dump(data, fp, indent=4)
                        updated_count += 1
                except Exception as ex:
                    print(f"Warning: Failed to set cast shadows for {bgsm_path.name}: {ex}")
    return updated_count

def run_conversion(
    input_files: List[Path],
    output_dir: Path,
    work_dir: Path,
    fnv_base_dir: Optional[Path] = None,
    nifskope_exe: Path = DEFAULT_NIFSKOPE_EXE,
    set_cast_shadows: bool = True
) -> Dict[str, Any]:
    """
    Executes automated conversion of New Vegas NIFs to Fallout 4 format.
    """
    manifest: Dict[str, Any] = {
        "timestamp": time.strftime("%Y-%m-%dT%H:%M:%S"),
        "converter_engine": str(nifskope_exe),
        "output_directory": str(output_dir),
        "files_requested": len(input_files),
        "success": False,
        "elapsed_seconds": 0.0,
        "models": []
    }
    
    if not nifskope_exe.exists():
        manifest["error"] = f"NifSkope converter not found at: {nifskope_exe}"
        return manifest
        
    start_total = time.perf_counter()
    
    # 1. Prepare work environment
    extracted_dir = work_dir / "extracted"
    temp_dir = work_dir / "temp"
    
    if work_dir.exists():
        shutil.rmtree(work_dir)
    extracted_dir.mkdir(parents=True, exist_ok=True)
    temp_dir.mkdir(parents=True, exist_ok=True)
    output_dir.mkdir(parents=True, exist_ok=True)
    
    # 2. Map and copy inputs to extracted/meshes
    file_map: Dict[str, Dict[str, Any]] = {}
    for inp in input_files:
        if not inp.exists():
            print(f"Error: Input file does not exist: {inp}")
            manifest["models"].append({
                "source": str(inp),
                "status": "FAILED_MISSING_INPUT"
            })
            continue
            
        header_info = inspect_nif_header(inp)
        if not header_info.get("is_fnv", False):
            print(f"Warning: {inp.name} does not match standard FNV header (user_ver: {header_info.get('user_ver')})")
            
        # Determine relative path
        if fnv_base_dir and inp.is_relative_to(fnv_base_dir):
            rel_path = inp.relative_to(fnv_base_dir)
            if rel_path.parts[0].lower() == "meshes":
                rel_parts = rel_path.parts[1:]
            else:
                rel_parts = rel_path.parts
        else:
            # Fallback: look for meshes in parts
            parts = inp.parts
            lower_parts = [p.lower() for p in parts]
            if "meshes" in lower_parts:
                m_idx = lower_parts.index("meshes")
                rel_parts = parts[m_idx+1:]
            else:
                rel_parts = (inp.name,)
                
        rel_mesh_path = Path(*rel_parts)
        dest_in_extracted = extracted_dir / "meshes" / rel_mesh_path
        dest_in_extracted.parent.mkdir(parents=True, exist_ok=True)
        shutil.copy2(inp, dest_in_extracted)
        
        file_map[inp.name] = {
            "source_path": inp,
            "rel_path": rel_mesh_path,
            "dest_in_extracted": dest_in_extracted,
            "input_header": header_info
        }
        print(f"Staged {inp.name} -> {dest_in_extracted.relative_to(work_dir)}")
        
    if not file_map:
        manifest["error"] = "No valid input files staged."
        return manifest
        
    # 3. Invoke NifSkope headless converter
    cmd = [
        str(nifskope_exe),
        "--convert",
        str(temp_dir),
        str(extracted_dir),
        str(extracted_dir)
    ]
    print(f"\n[FNV to FO4 Engine] Executing command:\n{' '.join(cmd)}")
    t0 = time.perf_counter()
    proc = subprocess.run(
        cmd,
        capture_output=True,
        text=True,
        cwd=str(nifskope_exe.parent)
    )
    conv_duration = time.perf_counter() - t0
    manifest["engine_duration_seconds"] = round(conv_duration, 4)
    manifest["engine_exit_code"] = proc.returncode
    manifest["engine_stdout"] = proc.stdout.strip()
    manifest["engine_stderr"] = proc.stderr.strip()
    
    print(f"NifSkope engine finished in {conv_duration:.2f}s with exit code {proc.returncode}")
    if proc.returncode != 0:
        manifest["error"] = f"Engine returned non-zero exit code {proc.returncode}: {proc.stderr}"
        return manifest
        
    # 4. Post-processing: set cast shadows
    shadows_updated = 0
    if set_cast_shadows:
        mat_dir = temp_dir / "Materials"
        if not mat_dir.exists():
            mat_dir = temp_dir / "materials"
        shadows_updated = process_bgsm_shadows(mat_dir)
        print(f"Applied bCastShadows to {shadows_updated} BGSM material descriptors.")
    manifest["shadows_updated"] = shadows_updated
    
    # 5. Locate and verify converted files in temp
    all_succeeded = True
    for name, item in file_map.items():
        rel_path = item["rel_path"]
        # In batch mode, NifSkope outputs to temp_dir / meshes / new_vegas / rel_path
        # or temp_dir / rel_path or temp_dir / name
        candidates = [
            temp_dir / "meshes" / "new_vegas" / rel_path,
            temp_dir / "meshes" / rel_path,
            temp_dir / rel_path,
            temp_dir / name
        ]
        
        found_output: Optional[Path] = None
        for cand in candidates:
            if cand.exists():
                found_output = cand
                break
                
        model_result: Dict[str, Any] = {
            "name": name,
            "source": str(item["source_path"]),
            "input_size_bytes": item["input_header"].get("size", 0),
            "input_user_ver": item["input_header"].get("user_ver"),
            "input_user_ver2": item["input_header"].get("user_ver2"),
        }
        
        if found_output and found_output.exists():
            out_header = inspect_nif_header(found_output)
            model_result["output_found"] = True
            model_result["output_size_bytes"] = out_header.get("size", 0)
            model_result["output_user_ver"] = out_header.get("user_ver")
            model_result["output_user_ver2"] = out_header.get("user_ver2")
            model_result["is_fo4_verified"] = out_header.get("is_fo4", False)
            
            # Copy to final output_dir
            final_mesh_path = output_dir / "meshes" / "new_vegas" / rel_path
            final_mesh_path.parent.mkdir(parents=True, exist_ok=True)
            shutil.copy2(found_output, final_mesh_path)
            model_result["final_mesh_path"] = str(final_mesh_path)
            model_result["status"] = "CONVERTED"
            print(f"SUCCESS: {name} converted to FO4 ({out_header.get('size')} bytes)")
        else:
            all_succeeded = False
            model_result["output_found"] = False
            model_result["status"] = "FAILED_NO_OUTPUT"
            print(f"FAILED: Output not found for {name}")
            
        manifest["models"].append(model_result)
        
    # 6. Copy generated materials to final output_dir
    src_mat_dir = None
    for cand in (temp_dir / "Materials", temp_dir / "materials"):
        if cand.exists():
            src_mat_dir = cand
            break
            
    if src_mat_dir and src_mat_dir.exists():
        final_mat_dir = output_dir / "materials"
        if final_mat_dir.exists():
            shutil.rmtree(final_mat_dir)
        shutil.copytree(src_mat_dir, final_mat_dir)
        mat_count = sum(len(f) for _, _, f in os.walk(final_mat_dir))
        manifest["materials_copied"] = mat_count
        print(f"Copied {mat_count} material files to {final_mat_dir}")
        
    manifest["success"] = all_succeeded
    manifest["elapsed_seconds"] = round(time.perf_counter() - start_total, 4)
    return manifest

def main():
    parser = argparse.ArgumentParser(description="Automated FNV to FO4 Asset Converter CLI")
    parser.add_argument("inputs", nargs="+", help="Input NIF files or folders")
    parser.add_argument("-o", "--output-dir", required=True, help="Destination directory for FO4 assets")
    parser.add_argument("-w", "--work-dir", default=r"D:\Downloads\Fallout Tools\automation_sandbox", help="Sandbox working directory")
    parser.add_argument("-b", "--fnv-base-dir", default=r"D:\Downloads\Fallout Tools\Sandcrete Kit-48139-1-1\Sandcrete Kit\Extract me\Fallout New Vegas\Data", help="Base directory for relative paths")
    parser.add_argument("-e", "--nifskope-exe", default=str(DEFAULT_NIFSKOPE_EXE), help="Path to NifSkope converter executable")
    parser.add_argument("--no-shadows", action="store_true", help="Do not enforce bCastShadows in BGSM materials")
    parser.add_argument("-m", "--manifest", help="Optional output JSON path for the execution manifest")
    
    args = parser.parse_args()
    
    # Collect input files
    all_inputs: List[Path] = []
    for inp_str in args.inputs:
        p = Path(inp_str)
        if p.is_file():
            all_inputs.append(p)
        elif p.is_dir():
            all_inputs.extend(list(p.rglob("*.nif")))
            
    if not all_inputs:
        print("Error: No input .nif files found.")
        sys.exit(1)
        
    print(f"Starting FNV to FO4 conversion for {len(all_inputs)} models...")
    res = run_conversion(
        input_files=all_inputs,
        output_dir=Path(args.output_dir),
        work_dir=Path(args.work_dir),
        fnv_base_dir=Path(args.fnv_base_dir) if args.fnv_base_dir else None,
        nifskope_exe=Path(args.nifskope_exe),
        set_cast_shadows=not args.no_shadows
    )
    
    if args.manifest:
        with open(args.manifest, "w", encoding="utf-8") as fp:
            json.dump(res, fp, indent=4)
        print(f"Manifest written to: {args.manifest}")
        
    print(f"\nConversion finished in {res['elapsed_seconds']}s. Success: {res['success']}")
    if not res["success"]:
        sys.exit(1)

if __name__ == "__main__":
    main()
