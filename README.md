# Fallout Remake — importação estrutural do templo

Projeto pessoal em Godot 4 .NET/C#, começando por Fallout 2. A direção visual futura é
um ambiente isométrico 3D inspirado em Wasteland 3. O leitor já interpreta a estrutura
do Temple of Trials e reconstrói seus bytes em memória; ainda não há cena 3D jogável.

**Correção do primeiro marco:** o interior é `maps/arcaves.map`, ID 3.
`maps/artemple.map`, ID 126, é a área externa com Klint. O inspetor agora usa o interior
como padrão. O cadastro local e os scripts dos objetos confirmaram essa distinção.

## Executar

Na raiz do repositório, com SDK .NET capaz de compilar `net8.0` e runtime .NET 8:

```powershell
dotnet build 'Fallout Remake.csproj'
dotnet run --project tools/TempleInspector -- --self-test
dotnet run --project tools/TempleInspector -- --data-dir 'D:\Games\fallout2\Fallout 2 English'
dotnet run --project tools/TempleInspector -- --data-dir 'D:\Games\fallout2\Fallout 2 English' --export-local
```

O último caminho foi confirmado no manifesto local da Epic em 16/10/2026. Em outra
máquina, informe explicitamente a pasta de idioma que contém `master.dat` e `critter.dat`.
Não selecione a pasta `Fallout1in2`. Para conferir a área externa, acrescente
`--map maps/artemple.map`.

Para testar o mesmo leitor dentro do Godot, após compilar:

```powershell
$godotExe = 'D:\Downloads\Godot_v4.7.2-stable_mono_win64\Godot_v4.7.2-stable_mono_win64\Godot_v4.7.2-stable_mono_win64_console.exe'
& $godotExe --headless --path . res://scenes/ImportSmoke.tscn -- --data-dir 'D:\Games\fallout2\Fallout 2 English'
```

Sucesso: `IMPORT_SMOKE_OK`, seguido de saída com código 0. O caminho acima foi obtido
do atalho local do Godot; ajuste-o em outra máquina. A cena é um diagnóstico sem janela.
O projeto continua sem cena principal de jogo.

O leitor abre os dados em modo de leitura e imprime um resumo JSON, com contagens,
origens, hashes e divergências dos dados originais. Sempre verifica uma reconstrução
byte a byte a partir dos campos lidos. O modo `--export-local` grava a representação
intermediária em `.local/arcaves.v2.json`, incluindo os bytes de origem e protótipos;
esse arquivo é privado, regenerável e ignorado pelo Git. Execute esse modo na raiz.
Não inclua dados originais, exportações ou arte convertida no repositório.

- [Formatos, limites e próximo incremento](docs/temple-import.md)
- [Validação atual: scripts, objetos e inventários](docs/validation-objects-2026-10-17.md)
- [Validação histórica do primeiro marco](docs/validation-2026-10-16.md)
