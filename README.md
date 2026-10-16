# Fallout Remake — primeiro marco

Projeto pessoal em Godot 4 .NET/C#, começando por Fallout 2. A direção visual futura é
um ambiente isométrico 3D inspirado em Wasteland 3. Este marco entrega a base de leitura
do Temple of Trials; ainda não há mapa 3D nem cena jogável.

## Executar

Na raiz do repositório, com SDK .NET capaz de compilar `net8.0` e runtime .NET 8:

```powershell
dotnet build 'Fallout Remake.csproj'
dotnet run --project tools/TempleInspector -- --self-test
dotnet run --project tools/TempleInspector -- --data-dir 'D:\Games\fallout2\Fallout 2 English'
```

O último caminho foi confirmado no manifesto local da Epic em 16/10/2026. Em outra
máquina, informe explicitamente a pasta de idioma que contém `master.dat` e `critter.dat`.
Não selecione a pasta `Fallout1in2`.

Para testar o mesmo leitor dentro do Godot, após compilar:

```powershell
$godotExe = 'D:\Downloads\Godot_v4.7.2-stable_mono_win64\Godot_v4.7.2-stable_mono_win64\Godot_v4.7.2-stable_mono_win64_console.exe'
& $godotExe --headless --path . res://scenes/ImportSmoke.tscn -- --data-dir 'D:\Games\fallout2\Fallout 2 English'
```

Sucesso: `IMPORT_SMOKE_OK`, seguido de saída com código 0. O caminho acima foi obtido
do atalho local do Godot; ajuste-o em outra máquina. A cena é um diagnóstico sem janela.
O projeto continua sem cena principal de jogo.

O leitor abre os dados em modo de leitura, mantém os bytes em memória e imprime um
resumo JSON. Para salvar relatórios locais, use `.local/` (ignorada pelo Git).
Não inclua arquivos originais, extrações ou arte convertida no repositório.

- [Formatos, limites e próximo incremento](docs/temple-import.md)
- [Validação do ambiente e resultados](docs/validation-2026-10-16.md)
