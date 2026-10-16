# Validação — 16/10/2026 (America/Bahia)

## Ambiente localizado

- Projeto: `C:\Dev\FalloutRemake`; inicialmente só os arquivos básicos de Godot,
  sem scripts/cenas e sem `.git`. Nenhum `AGENTS.md` encontrado no projeto ou ancestrais.
- Git iniciado com snapshot `85f804e` na `main`; trabalho na branch
  `milestone/temple-import-foundation`. Sem remote configurado ou publicação.
- SDK .NET **10.0.402**, runtime .NET **8.0.8**; projeto `net8.0`,
  `Godot.NET.Sdk/4.7.2`. Não foi necessário instalar software adicional.
- Godot **4.7.2.stable.mono.official.ed1daf0bf**, localizado pelo atalho do Desktop
  em `D:\Downloads\Godot_v4.7.2-stable_mono_win64\Godot_v4.7.2-stable_mono_win64`.
- Manifesto Epic: instalação em `D:\Games\fallout2`. Usado o conteúdo da pasta
  `Fallout 2 English`, não a conversão `Fallout1in2`.
- Cópia em `C:\Dev\FalloutReference\fallout2\Fallout 2 English`:
  `master.dat`, `critter.dat` e `patch000.dat` têm hashes idênticos aos da Epic.
- CE local: `C:\Dev\FalloutReference\fallout2-ce-windows-x64\fallout2-ce.exe`,
  junto de `ce.dat` e configuração de exemplo. Executável sem metadados de versão;
  não foi iniciado nem associado automaticamente à versão do código consultado.

## Resultados

| Verificação | Resultado |
| --- | --- |
| Compilação do projeto Godot antes e depois | Passou; 0 erros, 0 avisos na compilação final. |
| Oito grupos de testes sintéticos | Passaram: DAT puro/zlib, prefixo, bounds/truncamento, caminhos, duplicatas, tamanho descompactado, precedência, MAP/endian/flags/elevações/bytes preservados. |
| Leitura do Temple nos dados Epic | Passou; mapa 126, versão 20, 92.780 bytes; entrada 18492/0/0. |
| Execução C# dentro do Godot, sem janela | Passou; `IMPORT_SMOKE_OK`, código de saída 0. |
| Interpretação integral dos objetos/scripts | Não implementada; 52.544 bytes preservados em memória, ainda opacos. |
| Jogo original/CE, renderização 3D e combate | Não testados/não implementados neste marco. |

Comandos reproduzíveis no README. Relatório detalhado local em
`.local/temple-inspection.json`, ignorado pelo Git; nenhum recurso foi extraído.
Os testes criam somente dados sintéticos em `work/synthetic-*` e removem sua própria
pasta ao terminar. A cena `ImportSmoke.tscn` encerra automaticamente após o diagnóstico.

SHA-256 dos dados inspecionados:

| Arquivo | SHA-256 |
| --- | --- |
| `master.dat` | `9B096D3035EDAFD4077DEEB8EE7877A803B9DB98497AA4596624B5C058A84711` |
| `critter.dat` | `DA83D615967C0BAC41FBC50E6B86B3088401FF69ED9D8B2E5BE1D6AC0C125BCD` |
| `patch000.dat` | `16122A47D9FA1FB2F490D1FB38BB1B7EB8CA8EDCF2C06A2C9D78BEED812FF564` |
| `maps/artemple.map` descompactado | `11FC44F3FAD558F3CB9E12BF41018A023EF7DE8DEF365D516A44336157FEC050` |
| Executável CE local | `69B041902A11C19A3C4F39F322AC4C56DD7E7848612C1F1171A3BE29641A4ED6` |

Próximo passo: interpretar scripts/PRO/objetos e validar contagens/IDs antes de montar
a cena de inspeção 3D descrita em `temple-import.md`.
