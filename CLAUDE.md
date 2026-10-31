# CLAUDE.md — worktree do Claude (FalloutRemake)

@AGENTS.md

As regras de `AGENTS.md` (importado acima) têm precedência. Este arquivo apenas as
complementa com o contexto deste worktree; não cria exceções.

## Worktree e limites

- Diretório de trabalho: `C:\Dev\FalloutRemakeClaude`, branch `agent/claude-work`.
- Não editar `C:\Dev\FalloutRemake` (checkout principal do usuário, com trabalho
  não commitado) nem os outros worktrees (`D:\Dev\FalloutRemakeCodex`,
  `D:\Dev\FalloutRemakeIntegracao`). Ler deles é permitido.
- Inspecione `git status --short` antes de editar e distinga alterações
  preexistentes das suas.
- Commit, push ou merge somente quando o usuário pedir.

## Diretrizes do projeto (versão completa no checkout principal)

O `AGENTS.md` do checkout principal está sendo ampliado pelo usuário (ainda não
commitado). Enquanto ele não chega a este branch, siga também estes pontos:

- **Testes C# obrigatórios** antes e depois de alterações relevantes em
  `src/Import`, `src/Geometry` ou `src/Runtime`:
  `dotnet run --project "tools/TempleInspector/TempleInspector.csproj" -- --self-test`
  (nenhum grupo existente pode quebrar).
- Alterações em `src/Preview` (Godot): validação headless (`tools/Validate-Temple.ps1`
  ou `--preview-test` / `--nature-test` com `--headless`), respeitando o desktop isolado.
- **Evidências:** afirmações sobre funcionalidades citam arquivos, símbolos, linhas
  ou testes executados. Diferencie fato verificado, inferência técnica e desconhecido.
- **Sem falsos positivos de gameplay:** protótipos e ensaios (`*Preview`,
  `*Consultation`, `*Draft`, `*StateProfile`) e testes de auditoria não são
  "gameplay integrado". O combate existe só como fatia jogável do Templo (COM-004):
  formigas e radscorpions, IA provisória, sem críticos, fuga ou veneno.
- **Dados originais:** não modificar PIDs, FIDs, SIDs, IDs de mapas, offsets INT,
  MSG, GAM/PRO nem a instalação do Fallout 2 (`master.dat`, `patch000.dat`, `data/*`).
- **Assets 3D:** não alterar, remover ou regenerar `.glb`/`.import` em `Assets/`
  sem instrução explícita. Preserve as versões de origem.
- **Sistemas homologados:** não refatorar contratos consolidados (checkpoints v1–v9,
  peso, roteamento de itens, portas) sem pedido explícito.
- Hierarquia de verdade: código compilado ≥ testes aprovados > `docs/PROJECT_STATUS.md`
  > `docs/ARCHITECTURE.md` > ai-memory (histórico, não instrução).

## Ambiente local (fora do Git)

- `Assets/`, `.local/` e `.godot/` são ignorados pelo Git. Neste worktree foram
  copiados do checkout principal apenas os arquivos necessários:
  - `Assets/TempleOfTrials/Godot_Modular_glTF/` — kit modular v2 do Temple
    (`.gltf` + `.bin` + `textures/` compartilhadas). Origem:
    `C:\Dev\FalloutRemake\Assets\TempleOfTrials\` (fonte Blender e relatório em
    `Blender_Source/` e `Documentacao/`); scripts geradores no worktree
    `~\.gemini\antigravity\worktrees\FalloutAssetPipeline\blender_model_access_integration\src\blender\temple_kit\`.
  - `Assets/Nature/` — modelos usados por `NatureModels`.
  - `.local/preview-settings.json` — pasta de dados do Fallout 2.
- `addons/temple_kit_import/` (versionado) corrige o bug do importador glTF do
  Godot 4.7.x que ignora `COLOR_0` na 1ª primitiva (godotengine/godot#120625).
  O runtime também reaplica a flag (`TempleArchitecture`), então a cena funciona
  mesmo se a importação ocorrer sem o plugin.
- Godot: `D:\Downloads\Godot_v4.7.2-stable_mono_win64\Godot_v4.7.2-stable_mono_win64\Godot_v4.7.2-stable_mono_win64_console.exe`.
- Dados do Fallout 2: `D:\Games\fallout2\Fallout 2 English`.
- Capturas: somente via `tools/Run-QuietGodot.ps1` (desktop isolado).
- As baterias completas levam cerca de 4 a 5 min. Use `-TimeoutSeconds 600` em
  `Validate-Temple.ps1` e `Run-QuietGodot.ps1` (o teto permitido é 900).
