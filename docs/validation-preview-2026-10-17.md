# Terceiro marco — prévia 3D, 17/10/2026 (America/Bahia)

Branch `milestone/temple-3d-preview`, a partir de `ae7c3c1`.
Godot 4.7.2 .NET, .NET SDK 10.0.402, alvo net8.0. Dados locais Epic já confirmados:
`D:\Games\fallout2\Fallout 2 English`. Nenhum asset original foi adicionado ao Git.

## Resultado

Cena principal com câmera ortográfica, movimento/zoom/rotação, entrada, enquadramento,
troca de elevação, filtros e seleção de objetos/piso. O inspetor mostra IDs, posição,
rotação, script, flags, inventário e destinos de saída. Configuração salva em `.local/`.

| Elevação | Células de piso exibidas | Objetos raiz posicionados | Registros de inventário fora da cena |
| --- | ---: | ---: | ---: |
| 0 | 4595 | 1842 | 2 |
| 1 | 4200 | 1387 | 3 |
| 2 | 4566 | 1299 | 3 |

Com os filtros iniciais, 1345 dos 1842 objetos da elevação 0 ficam visíveis.
Entrada: hex 28906, elevação 0, rotação 0. Porta usada no teste de seleção:
ID 102, offset 137396, hex 11108, PID 33555366, FID 33555377.

## Verificações executadas

- Compilação do projeto: **zero erros e zero avisos**.
- **22 grupos de testes sintéticos passaram**: 18 do leitor mais 4 de geometria.
  Novos testes cobrem âncoras da projeção CE, paridades, seis vizinhos/rotações,
  alinhamento piso/hex, transformação inversa e rejeição de índices fora dos limites.
- `--preview-test` executado sem janela e com renderização D3D12/Forward+ na
  NVIDIA GeForce RTX 5060 Ti. Ambos concluíram com código 0.
- Conferidos: contagem de nós, metadados/IDs, posições, raio vertical, seleção da
  porta pela projeção da câmera, filtros removendo seleção/colisão de consulta,
  troca de elevações 0→1→2→0, retorno à entrada e rotação da câmera por Q/E.
- Reconstrução byte a byte do MAP continua aprovada, antes e depois da prévia.
- Pasta inexistente com `--preview-test`: erro explícito e saída **1**, sem travar.
- Duas capturas 1440×900 revisadas: entrada e porta selecionada. Cena, rótulos,
  filtros e inspeção legíveis; detalhes extensos têm rolagem própria.
- `git diff --check` sem problemas. Dados, configuração, imagens e logs em `.local/`
  ficam ignorados. Apenas código, cena, configuração do projeto e documentação são versionados.

Saídas principais do teste:

```text
PREVIEW_LAYER_OK: elevation 1; 4200 floor cells; 1387 source objects; 3 inventory records retained outside scene.
PREVIEW_LAYER_OK: elevation 2; 4566 floor cells; 1299 source objects; 3 inventory records retained outside scene.
PREVIEW_TEST_OK: elevation 0; 4595 floor cells; 1842 source objects; metadata, transforms, camera picking/rotation, filters, elevation switching and source bytes verified.
```

Comandos reproduzíveis no README. Capturas locais: `.local/temple-preview.png` e
`.local/temple-preview.selection.png`; logs: `.local/preview-render*.log`.
O seletor nativo de pastas e cada gesto físico do mouse não foram exercitados
manualmente; carregamento via configuração, seleção por câmera e ações indicadas
acima foram exercitados pelo teste dentro do motor.

## Limites e próximo passo

Formas e alturas provisórias, sem confirmação visual no jogo original. Colisões são
apenas para consulta; não representam bloqueio ou passabilidade. Nenhum script INT é
executado e nenhum personagem é controlável. Próximo marco: classificar hexes
caminháveis usando flags/portas/bloqueadores e preparar uma navegação mínima na
elevação 0. Combate e acabamento visual continuam fora deste incremento.
