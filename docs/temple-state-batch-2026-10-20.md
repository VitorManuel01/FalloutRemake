# Bateria de estados e conversa — 20/10/2026

Continuação da base `90d134b`, após os doze incrementos da
[bateria anterior](continuation-batch-2026-10-20.md). Os commits permanecem na
mesma linhagem de branches `codex/`, sem push.

| Commit | Incremento |
| --- | --- |
| `9138cfb` | Banco transacional de variáveis locais, de mapa e globais, com limites e identidade fonte; acesso ao local 16 recusado. |
| `dacf1ca` | Leitor GAM e perfil original de inicialização conferidos nos dados Epic e no CE local. |
| `f533358` | Porta final utilizável antes do desafio; checkpoint v5, com migração protegida dos formatos v1–v4. |
| `e6b8bda` | Cena valida porta aberta, passagem ocupada, checkpoint e chegada ao lado do NPC. |
| `4726812` | Cinco escolhas verificadas do ramo Speech, em um estado separado da partida. |
| `0b37548` | Arquivo v1 do ensaio, com leitura estrita, fontes vinculadas e substituição atômica. |
| `bf4609f` | Consulta do MSG original na cena; texto composto corretamente ao redor do nome do personagem. |
| `f875285` | Salvar/carregar o resultado pela janela, com recusa de sobrescrita parcial e callbacks antigos. |
| `b3a7b20` | Botão Chegar ao NPC: percurso alcançável, parada adjacente e conversa aberta separadamente. |
| `0ffda4a` | Auditoria posterior: fases de movimento, evento de dois segundos, porta, ocultação e ausência de concessão direta de XP nos dois programas. |
| `5efe9a9` | Política explícita para NPC atravessar saídas sem removê-las; padrão do jogador preservado, última perna validada em 31 passos. |

O relatório adicional `--temple-aftermath-report` confere a saída original do
NPC após aceitação, com fases, porta, evento de dois segundos e ocultação final.
Nenhum XP foi inventado: os dois programas examinados não têm chamada direta
para concessão de experiência. A terceira perna do NPC está inalcançável na
geometria atual da prévia: o diagnóstico encontrou uma rota de 31 passos quando
os marcadores de saída deixam de encerrar a busca. A confirmação no CE permitiu
uma política explícita para grafos de NPC, preservando saídas e bloqueadores.
A navegação ativa continua com a política anterior do jogador.

## Uso atual

Abra a porta final na elevação 2, selecione o NPC e use **Chegar ao NPC**.
Ao chegar, **Conversa · ensaio** apresenta cinco escolhas do texto da instalação
local, com INT 5 e Speech marcado no ensaio. O protótipo mantém sua identidade
original; a descrição de Cameron é mostrada separadamente do nome Dumar.

Concluir, cancelar, recomeçar, salvar ou carregar o ensaio não altera o checkpoint
ativo, itens, XP, portas nem posição do NPC. O arquivo separado contém somente
metadados e valores; nenhum texto MSG ou asset original.

## Validações e limites

275 grupos sintéticos (+26 sobre a base), sete grupos do validador de logs e
builds sem avisos/erros. A bateria completa de 16 etapas passou após a consulta
inicial: `.local/temple-validation-20261020-210743-43c0e60e/manifest.json`.
A versão final passou **17/17 etapas**, manifesto
`.local/temple-validation-20261020-211634-17595cc5/manifest.json`.
Os 139 links locais da documentação foram conferidos, sem destinos ausentes.

As integrações verificam igualdade integral do checkpoint ativo em cada escolha,
cancelamento/reabertura, recomeço, arquivos inválidos, token novo, callback de
janela antiga, rota exata e preservação dos dois itens reais/25 XP do fixture.
As capturas isoladas em 1280×720 e 1024×768 passaram, com logs limpos e desktop
de entrada `Default` preservado. Nenhuma instância do usuário foi manipulada.
A execução gráfica final completa também passou em 1280×720, em 39,2 segundos:
`.local/temple-state-final-isolated.json`, `Passed=true`, `LogFailure=null`,
desktop de entrada `Default` antes e depois.

Combate geral, duelo, custódia de itens do desafio, execução geral de INT e
conversa com efeitos na partida continuam indisponíveis. Fallout 1 fica para
depois. `Assets/`, `.uid` e a formatação preexistente de `TemplePreview.cs`
continuam preservados fora dos commits.

Próximo marco: definir posição, bloqueador, fase e evento do NPC persistidos
como uma transação restaurável, usando a política explícita de navegação.
Recomendo
**GPT-6.1 Sol, esforço Alto**.

Detalhes: [porta e v5](initial-temple-gate-v5-2026-10-20.md),
[ensaio](temple-speech-draft-2026-10-20.md),
[arquivo separado](temple-speech-state-file-2026-10-20.md),
[consulta](temple-speech-consultation-2026-10-20.md),
[salvar/carregar](temple-speech-save-ui-2026-10-20.md),
[percurso](temple-npc-approach-2026-10-20.md),
[sequência posterior](temple-speech-aftermath-2026-10-20.md),
[política de saídas para NPC](npc-exit-traversal-policy-2026-10-20.md).
