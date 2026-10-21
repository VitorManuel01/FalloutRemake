# Bateria: saída restaurável do NPC — 20/10/2026

Branch `codex/temple-npc-departure`, a partir de `faae389`, sem push.

| Commit | Marco entregue |
| --- | --- |
| `943d61a` | Estado transacional: posição, rotação, bloqueador, porta, fase e evento derivados de um snapshot; rotas originais 32/28/31 e recusa de ocupação/repetição. |
| `7e3ab23` | Arquivo próprio com leitura estrita, restauração completa, token novo e gravação atômica. |
| `ad532ab` | Consulta Speech abre ensaio manual com traçado rotulado, passos, paradas, espera, salvar/carregar e retorno à conversa. |
| `72e93da` | Vista 3D em mundo separado: cenário de referência, NPC destacado, câmera própria, porta e ocultação final. |

## Como conferir

Na elevação 2, aproxime-se do NPC e conclua ou carregue as cinco escolhas em
**Conversa · ensaio**. Abra **Saída do NPC · ensaio**. Avance um passo ou um trecho,
cumpra a espera da porta e alterne **Ver em 3D / Ver traçado**. É possível salvar
qualquer fase, inclusive no meio da espera, e restaurá-la depois. Este arquivo
fica separado da partida e do resultado Speech.

## Validação

- **285 grupos sintéticos** (+10), sete grupos do validador de logs e builds sem
  avisos/erros.
- **18/18 etapas**: dois builds, testes, 13 relatórios reais e integração Godot,
  além do validador de logs. Manifesto:
  `.local/temple-validation-20261021-023342-a11e8cdd/manifest.json`.
- Relatório `--temple-npc-departure-report`: 91 passos adjacentes, 93 restaurações,
  dois hexes de saída cruzados, 22 marcadores conservados e MAP reconstruído.
- Interface verifica ações antigas, conversa oculta, arquivo inválido, espera
  parcial, retorno/reabertura e igualdade integral do checkpoint ativo.
- Vista 3D verifica mundo próprio, identidade, pose, porta e visibilidade;
  transformações e navegação ativas preservadas.
- Capturas isoladas 1280×720 e 1024×768 aprovadas. A versão visual final foi
  recompilada e validada em 1024×768: `.local/npc-departure-3d-final-isolated.json`,
  `Passed=true`, `LogFailure=null`, entrada `Default` antes e depois.

Os horários dos nomes dos manifestos são UTC; a bateria foi realizada na noite
de 20/10 no fuso America/Bahia. Dados Epic e executável Godot foram usados somente
nos caminhos já documentados/configurados. Os assets, `.uid` e a formatação
preexistentes continuam fora dos commits; nenhum asset proprietário foi adicionado.

O ai-memory respondeu na retomada. A visão geral consultada é
`concepts/project-overview.md`, `page_id=01a15c12-01d9-7772-9985-bcb9bbe76845`.

## Limite atual e próximo marco

A aceitação com efeitos na partida ainda depende de uma transação do mundo.
O ensaio não concede XP, não resolve duelo e não executa uma VM geral.
O contrato do próximo incremento está em
[integração da saída no mundo](next-npc-world-transaction-2026-10-20.md).
Recomendo **GPT-6.1 Sol, esforço Alto** para a integração e revisão de rollback
([modelo na documentação oficial](https://developers.openai.com/api/docs/models/gpt-6.1-sol)).

Detalhes: [estado](temple-npc-departure-2026-10-20.md),
[arquivo](temple-npc-departure-file-2026-10-20.md),
[interface](temple-npc-departure-ui-2026-10-20.md),
[vista 3D](temple-npc-departure-3d-2026-10-20.md).
