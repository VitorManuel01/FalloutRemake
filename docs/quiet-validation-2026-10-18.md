# Vigésimo segundo marco — validação gráfica isolada

Branch `codex/quiet-godot-validation`, base `9ca072e`.

O usuário está realizando outros testes no computador. As validações passam a
usar `--headless` ou um desktop isolado do Windows; não se abre o Godot no desktop
ativo. A preferência também está registrada em `AGENTS.md`.

A instalação atual lista `headless` apenas com renderer `dummy`, sem captura de
viewport. `Run-QuietGodot.ps1` cria um desktop `CodexFallout-<GUID>` e inicia o
console do Godot com `STARTUPINFO.lpDesktop` apontando para ele. Não chama
`SwitchDesktop`, não solicita essa permissão e não injeta entrada. Console sem
janela, prioridade abaixo do normal, áudio Dummy e limite de 30 FPS.

O processo possui timeout (90 s por padrão, máximo 300 s). Somente o processo
recém-criado pode ser encerrado pelo runner. Handles e desktop são liberados ao
final; falhas não acionam fallback para o desktop normal. Logs/capturas em um
caminho explícito, com stdout, stderr e resumo do resultado. Não controla nem
encerra o editor ou outras instâncias do usuário.

Exemplo, na raiz do projeto:

```powershell
.\tools\Run-QuietGodot.ps1 -GodotExe '<caminho explícito do Godot console .NET>' -ProjectPath $PWD.Path -CapturePath "$PWD\.local\temple-isolated.png"
```

Validação: execução gráfica completa com código 0 em aproximadamente 15,5 s;
desktop de entrada `Default` antes/depois, stderr vazio e capturas geradas.
`.local/temple-isolated.exit-preview.png` revisada. Logs
`.local/temple-isolated.stdout.log`, `.stderr.log` e `.local/isolated-run.json`.
Os 114 grupos sintéticos e a integração anterior continuam disponíveis sem janela.
Timeout de 1 s também verificado: código 124 em aproximadamente 1,03 s, somente
a instância de teste encerrada, desktop de entrada `Default` antes/depois.

Este isolamento evita que a janela de teste apareça no desktop de entrada; a
renderização ainda utiliza recursos do computador. Não altera resolução, monitores,
atalhos do editor ou a sessão do usuário.

Próximo: enquadramento do percurso pela câmera. Modelo: GPT-6.1 Sol, esforço Alto (High).
