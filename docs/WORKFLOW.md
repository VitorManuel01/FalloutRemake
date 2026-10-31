# Guia de Workflow e Desenvolvimento — FalloutRemake

> **Referência Obrigatória:** [AGENTS.md](../AGENTS.md)  
> **Status dos Sistemas:** [PROJECT_STATUS.md](PROJECT_STATUS.md)

Este guia documenta os comandos reais, verificados e reprodutíveis para compilação, testes, validação de assets e execução no projeto **FalloutRemake**.

---

## 1. Princípios Operacionais e Regras de Segurança

1. **Regra de Ouro do Desktop Isolado:**
   - **NUNCA** abra instâncias gráficas do Godot ou do jogo no desktop ativo do usuário.
   - Para validações automatizadas de engine, use sempre o modo `--headless`.
   - Para capturas de tela visuais ou inspeções em janela, execute obrigatoriamente [tools/Run-QuietGodot.ps1](../tools/Run-QuietGodot.ps1) (que inicializa a instância em um desktop virtual Win32 isolado sem trocar o desktop de entrada do usuário).
2. **Protocolo C# (.NET 8):**
   - Execute a suíte de testes sintéticos **antes e depois** de qualquer alteração relevante em C#:
     ```powershell
     dotnet run --project "tools/TempleInspector/TempleInspector.csproj" -- --self-test
     ```
   - O projeto possui 335 grupos de teste sintéticos que rodam em ~1,4 segundo (tempo medido) e não dependem do Godot.

---

## 2. Configuração do Ambiente e Dependências

### Dependências Necessárias
* **.NET 8 SDK** (para compilar tanto o jogo quanto as ferramentas CLI).
* **Godot 4.7 Mono (C#)**: Utilizar a versão console (`*_console.exe`) para saída direta em terminal.
* **Instalação clássica de Fallout 2**: Diretório contendo os arquivos originais (`master.dat`, `patch000.dat` e pasta `data/`).

### Configuração de Caminhos Locais
O jogo e as ferramentas leem os caminhos locais a partir de `.local/preview-settings.json` na raiz do projeto (arquivo ignorado pelo Git). Exemplo de configuração nesta estação de trabalho:

```json
{
    "GameDataDirectory": "D:\\Games\\fallout2\\Fallout 2 English"
}
```

> [!NOTE]
> Se o arquivo `.local/preview-settings.json` não existir em uma nova máquina, a interface de prévia solicitará que o usuário aponte a pasta de dados ou você poderá criá-lo manualmente com o caminho correspondente.

---

## 3. Comandos de Compilação e Execução

### A. Compilação dos Projetos (.NET 8)
Comandos 100% portáveis que funcionam em qualquer ambiente com .NET 8 SDK:

```powershell
# 1. Compilar o projeto principal do Godot (.NET / C#)
dotnet build "Fallout Remake.csproj" -v m --nologo
# Tempo medido: ~1,1s

# 2. Compilar a ferramenta de console TempleInspector
dotnet build "tools/TempleInspector/TempleInspector.csproj" -v m --nologo
# Tempo medido: ~1,0s
```

---

### B. Testes Sintéticos Unitários (< 2 segundos)
Totalmente portável. Testa os subsistemas `src/Import`, `src/Geometry` e `src/Runtime` sem inicializar o Godot e sem precisar dos dados originais:

```powershell
dotnet run --project "tools/TempleInspector/TempleInspector.csproj" -- --self-test
```
*Saída esperada:* `335 synthetic test groups passed.` (tempo medido: ~1,4s).

---

### C. Relatórios de Auditoria de Dados Originais
Requer o caminho da instalação original de Fallout 2 (passado via `--data-dir`):

```powershell
# Execução direta via DLL compilada
$DATA_DIR = "D:\Games\fallout2\Fallout 2 English"

# Relatório de estado do templo e portas (tempo estimado: ~0,5s)
dotnet "tools/TempleInspector/bin/Debug/net8.0/TempleInspector.dll" --data-dir $DATA_DIR --temple-state-report

# Relatório de diálogo e dependências da Anciã de Arroyo (tempo estimado: ~0,5s)
dotnet "tools/TempleInspector/bin/Debug/net8.0/TempleInspector.dll" --data-dir $DATA_DIR --arroyo-elder-report

# Relatório de baús e contêineres originais (tempo estimado: ~0,5s)
dotnet "tools/TempleInspector/bin/Debug/net8.0/TempleInspector.dll" --data-dir $DATA_DIR --temple-chest-report
```

---

### D. Validação Integrada Completa (Suíte de 20 Etapas)
Orquestra builds, 335 testes sintéticos, 14 relatórios de dados e testes in-engine do Godot em modo `--headless`:

```powershell
$GODOT = "D:\Downloads\Godot_v4.7.2-stable_mono_win64\Godot_v4.7.2-stable_mono_win64\Godot_v4.7.2-stable_mono_win64_console.exe"
$DATA_DIR = "D:\Games\fallout2\Fallout 2 English"

powershell -File tools/Validate-Temple.ps1 -GodotExe $GODOT -DataDirectory $DATA_DIR
```
*Saída esperada:* JSON consolidado com `Passed: True` e 20 etapas aprovadas (tempo medido no manifesto mais recente: ~88 segundos, registrado em `.local/temple-validation-<data>-<id>/manifest.json`).

> [!WARNING]
> A aprovação no `Validate-Temple.ps1` atesta que os métodos assíncronos in-engine completaram e que não houve exceções não tratadas ou mensagens de erro registradas no log. **Ela não atesta fidelidade visual gráfica** (pois roda em modo `--headless`) **nem ausência de vazamentos de memória** (nenhum profiler de heap é executado).

---

### E. Testes Pontuais in-Engine (Headless)
Você pode rodar testes específicos do Godot sem executar toda a suíte de 20 etapas:

```powershell
$GODOT = "D:\Downloads\Godot_v4.7.2-stable_mono_win64\Godot_v4.7.2-stable_mono_win64\Godot_v4.7.2-stable_mono_win64_console.exe"
$DATA_DIR = "D:\Games\fallout2\Fallout 2 English"

# 1. Teste de modelos e picking de Natureza (tempo medido: ~3,3s)
& $GODOT --headless --path "C:\Dev\FalloutRemake" -- --data-dir $DATA_DIR --nature-test

# 2. Teste completo de regras e fluxo de prévia (tempo medido: ~55s)
& $GODOT --headless --path "C:\Dev\FalloutRemake" -- --data-dir $DATA_DIR --preview-test
```

---

### F. Importação Headless de Assets 3D no Godot
Sempre que novos arquivos `.glb` forem adicionados ou modificados em `Assets/`, execute o editor do Godot em modo headless para gerar/atualizar os metadados `.import`:

```powershell
$GODOT = "D:\Downloads\Godot_v4.7.2-stable_mono_win64\Godot_v4.7.2-stable_mono_win64\Godot_v4.7.2-stable_mono_win64_console.exe"

& $GODOT --headless --path "C:\Dev\FalloutRemake" --editor --quit
```

---

## 4. Matriz de Escolha de Testes

Utilize esta matriz para escolher a validação adequada para o escopo da sua tarefa, minimizando o tempo de espera:

| Escopo da Modificação | Teste Recomendado | Tempo (Medido / Estimado) | Pré-requisitos | O que efetivamente comprova |
| :--- | :--- | :--- | :--- | :--- |
| **Documentação (`docs/`, `*.md`)** | `dotnet build` dos dois projetos | ~2,1s *(medido)* | Apenas .NET 8 SDK | Ausência de quebras de compilação C# |
| **Domínio (`src/Import`, `src/Geometry`, `src/Runtime`)** | `TempleInspector --self-test` | ~1,4s *(medido)* | Apenas .NET 8 SDK | 335 regras unitárias, parsers e migrações de checkpoint |
| **Dados / Scripts / Parsers (`src/Import`)** | Testes sintéticos + `--<relatorio>-report` | ~3s *(estimado)* | .NET 8 + pasta do Fallout 2 | Conformidade com offsets originais e reconstrução byte a byte (`MapLayoutWriter.Verify`) |
| **Apresentação / HUD / Câmera (`src/Preview`)** | `Godot --headless --preview-test` | ~55s *(medido)* | .NET 8 + Godot + pasta do Fallout 2 | Execução dos métodos in-engine sem crash ou erros no log |
| **Assets 3D / Shaders (`Assets/`, `NatureModels.cs`)** | `Godot --headless --nature-test` | ~3,3s *(medido)* | .NET 8 + Godot + pasta do Fallout 2 | Carregamento dos modelos de natureza e picking 3D |
| **Fechamento de Marco / PR** | `tools/Validate-Temple.ps1` completo | ~88s *(medido)* | Suíte completa (20 etapas) | Pipeline sequencial completo aprovado com manifesto |

---

## 5. Boas Práticas para Agentes de IA

1. **Preservação de Dados Originais:** Nunca altere ou sobrescreva arquivos em `D:\Games\fallout2\...`. Qualquer alteração deve ocorrer exclusivamente no repositório de trabalho.
2. **Arquivos Locais e Git:** Não comite arquivos sob `.local/`, binários em `bin/` ou `obj/`, nem arquivos temporários criados para testes.
3. **Padrão de Código:** O código de domínio deve continuar estritamente desacoplado do Godot. Não adicione `using Godot;` dentro de `src/Import`, `src/Geometry` ou `src/Runtime`.
