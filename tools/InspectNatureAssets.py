"""Read-only GLB inventory for the local Nature category; emits metadata, never assets."""
import hashlib
import json
from pathlib import Path
import struct

root = Path(__file__).resolve().parent.parent / "Assets" / "Nature"
rows = []
for path in sorted(root.glob("*.glb")):
    raw = path.read_bytes()
    magic, version, size, chunk_size, chunk_type = struct.unpack_from("<5I", raw)
    if magic != 0x46546C67 or version != 2 or size != len(raw) or chunk_type != 0x4E4F534A:
        raise ValueError(f"Unsupported GLB: {path.name}")
    doc = json.loads(raw[20:20 + chunk_size])
    primitives = [p for mesh in doc.get("meshes", []) for p in mesh["primitives"]]
    if any(p.get("mode", 4) != 4 for p in primitives):
        raise ValueError(f"Non-triangle geometry: {path.name}")
    references = [image["uri"] for image in doc.get("images", []) if "uri" in image]
    if any(Path(uri).name != uri or not (root / uri).is_file() for uri in references):
        raise ValueError(f"Dependency escapes or is absent from Nature: {path.name}")
    rows.append(dict(file=path.name, bytes=len(raw), sha256=hashlib.sha256(raw).hexdigest().upper(),
        primitives=len(primitives), materials=len(doc.get("materials", [])),
        triangles=sum(doc["accessors"][p["indices"]]["count"] // 3 if "indices" in p else doc["accessors"][p["attributes"]["POSITION"]]["count"] // 3 for p in primitives),
        position_bounds=[{key: doc["accessors"][p["attributes"]["POSITION"]].get(key) for key in ("min", "max")} for p in primitives],
        nodes=[{key: node[key] for key in ("name", "mesh", "translation", "rotation", "scale", "matrix") if key in node} for node in doc.get("nodes", [])],
        external_images=references, embedded_images=sum("bufferView" in i for i in doc.get("images", [])),
        extensions=doc.get("extensionsUsed", [])))
print(json.dumps(dict(category="Nature", assets=rows), indent=2))
