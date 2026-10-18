# Fallout Remake — prévia 3D do templo

Projeto pessoal em Godot 4 .NET/C#, começando por Fallout 2. A direção visual futura é
um ambiente isométrico 3D inspirado em Wasteland 3. A cena principal já permite
explorar o Temple of Trials com formas provisórias, inspecionar os objetos originais
e mover um marcador por hexágonos válidos na elevação 0. A primeira porta pode ser
aberta e fechada, atualizando os caminhos sem alterar os dados originais.
O leitor verifica a reconstrução dos bytes do mapa antes de montar a cena.
Ainda não há personagem com regras de jogo, combate ou arte final.

**Correção do primeiro marco:** o interior é `maps/arcaves.map`, ID 3.
`maps/artemple.map`, ID 126, é a área externa com Klint. O inspetor agora usa o interior
como padrão. O cadastro local e os scripts dos objetos confirmaram essa distinção.

## Executar

Abra `project.godot` no **Godot .NET** e pressione **F5**. A prévia lê a pasta
salva em `.local/preview-settings.json` (configurada nesta máquina). Em outra máquina,
use **Abrir dados** e selecione a pasta de idioma que contém `master.dat` e `critter.dat`.
A escolha fica salva apenas localmente; não é necessário extrair ou exportar o MAP.

- **WASD / botão direito:** mover a câmera; **roda:** zoom; **Q/E:** girar.
- **F / Entrada:** voltar à entrada na elevação 0; **Home / Ver tudo:** enquadrar a camada.
- **Clique:** consultar objeto ou piso; painel lateral: elevações 0–2 e filtros.
- **Próximo no mesmo hex:** alternar objetos sobrepostos sem confundir seus IDs.
- **M / Caminhar:** alternar para caminhada na elevação 0; clique no chão para mover
  o marcador azul. Pontos verdes indicam alcance, vermelhos bloqueios e âmbar saídas.
- **Portas:** no modo Caminhar, clique na primeira porta para se aproximar; depois
  use **U / Abrir porta**. Clique além dela para atravessar. U também permite fechar.
- **Reiniciar prévia:** voltar à entrada e restaurar as portas. Trocar de elevação
  reinicia o marcador, preservando as portas; as elevações 1–2 são apenas para inspeção.

Paredes são volumes de referência nos hexes originais. Alturas, dimensões e cores
são provisórias. O bloqueio usa FID/flags dos objetos, sem depender do piso ou dos
filtros visuais. Portas fechadas bloqueiam; saídas encerram o percurso sem trocar
de mapa. A interação exige proximidade e marcador parado. Portas trancadas ou com
scripts ainda não podem ser usadas; uma passagem ocupada não pode ser fechada.

Na raiz do repositório, com SDK .NET capaz de compilar `net8.0` e runtime .NET 8:

```powershell
dotnet build 'Fallout Remake.csproj'
dotnet run --project tools/TempleInspector -- --self-test
dotnet run --project tools/TempleInspector -- --data-dir 'D:\Games\fallout2\Fallout 2 English'
dotnet run --project tools/TempleInspector -- --data-dir 'D:\Games\fallout2\Fallout 2 English' --export-local
dotnet run --project tools/TempleInspector -- --data-dir 'D:\Games\fallout2\Fallout 2 English' --navigation

dotnet run --project tools/TempleInspector -- --data-dir 'D:\Games\fallout2\Fallout 2 English' --script scripts/doorl100.int

dotnet run --project tools/TempleInspector -- --data-dir 'D:\Games\fallout2\Fallout 2 English' --door-adapter-test
```

O último caminho foi confirmado no manifesto local da Epic em 16/10/2026. Em outra
máquina, informe explicitamente a pasta de idioma que contém `master.dat` e `critter.dat`.
Não selecione a pasta `Fallout1in2`. Para conferir a área externa, acrescente
`--map maps/artemple.map`.

Para testar a prévia e o leitor dentro do Godot, após compilar:

```powershell
$godotExe = 'D:\Downloads\Godot_v4.7.2-stable_mono_win64\Godot_v4.7.2-stable_mono_win64\Godot_v4.7.2-stable_mono_win64_console.exe'
& $godotExe --headless --path . -- --data-dir 'D:\Games\fallout2\Fallout 2 English' --preview-test
& $godotExe --headless --path . res://scenes/ImportSmoke.tscn -- --data-dir 'D:\Games\fallout2\Fallout 2 English'
```

Sucesso: `PREVIEW_TEST_OK`, `NAVIGATION_TEST_OK` e `DOOR_TEST_OK` / `IMPORT_SMOKE_OK`,
seguidos de saída com código 0.
O caminho do Godot foi obtido do atalho local; ajuste-o em outra máquina.
Para capturar a cena, execute sem `--headless` e acrescente
`--capture .local/temple-preview.png` após `--`. Com `--preview-test`, também é gerada
uma captura `.selection.png` da porta selecionada, `.navigation.png` do percurso
e `.door.png` da passagem aberta. Todas ficam fora do Git.

O leitor abre os dados em modo de leitura e imprime um resumo JSON, com contagens,
origens, hashes e divergências dos dados originais. Sempre verifica uma reconstrução
byte a byte a partir dos campos lidos. O modo `--export-local` grava a representação
intermediária em `.local/arcaves.v2.json`, incluindo os bytes de origem e protótipos;
esse arquivo é privado, regenerável e ignorado pelo Git. Execute esse modo na raiz.
Não inclua dados originais, exportações ou arte convertida no repositório.

O modo `--script` inspeciona a estrutura de um INT sem executá-lo. Acrescente
`--instructions` para incluir opcodes e operandos brutos. Os trechos são físicos,
sem prova automática de fluxo ou comportamento. A análise da segunda porta confirmou
que o script a tranca na entrada, embora o MAP grave o estado destrancado. Sua
interação continua indisponível na prévia até integrar um perfil de personagem.

O modo `--door-adapter-test` já valida a inicialização, tranca e arrombamento da
segunda porta com resultados determinísticos. Sucesso destranca e concede 25 XP uma
vez; falha crítica emperra e impede nova tentativa. A sessão mantém os dados fonte
intactos. O comando emite `TEMPLE_ADAPTER_TEST_OK`; não representa uma rolagem jogável.

- [Formatos, limites e próximo incremento](docs/temple-import.md)
- [Coordenadas e limites da prévia 3D](docs/preview-3d.md)
- [Navegação: regras e limites](docs/navigation.md)
- [Portas: comportamento e validação atual](docs/doors-2026-10-17.md)
- [Segunda porta: condições do script e leitor INT](docs/door-script-2026-10-18.md)
- [Adaptador de arrombamento: regras e validação](docs/door-adapter-2026-10-18.md)
- [Validação do quarto marco: caminhada por hexágonos](docs/validation-navigation-2026-10-17.md)
- [Validação do terceiro marco: prévia 3D](docs/validation-preview-2026-10-17.md)
- [Validação do segundo marco: scripts, objetos e inventários](docs/validation-objects-2026-10-17.md)
- [Validação histórica do primeiro marco](docs/validation-2026-10-16.md)
