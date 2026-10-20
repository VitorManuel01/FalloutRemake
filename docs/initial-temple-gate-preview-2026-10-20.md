# Porta final e aproximação ao NPC na cena

A interação U existente agora funciona na porta final do perfil inicial. O
inspetor explica que isso vale antes do desafio; conversa e duelo continuam
indisponíveis. Não muda a posição ou identidade do NPC, cujo nome de protótipo
continua Dumar. Nenhum diálogo foi executado para substituir esse nome.

`INITIAL_GATE_PREVIEW_OK` caminha até a porta, usa a interação de produção,
confere 1321 hexes alcançáveis, recusa fechamento ocupado, salva/carrega sobre
o antigo bloqueio e caminha até o vizinho 13928 do NPC. Confere checkpoint nessa
posição, mantém os dois itens reais e 25 XP, e restaura todo o estado anterior
da fixture no final. O movimento termina pelo caminho normal da prévia, sem
teleporte ou HUD de caminhada pendente.

Build sem avisos/erros e integração headless aprovados. A captura gráfica em
1280×720 também passou: `.local/initial-gate-isolated.json`, `Passed=true`,
`LogFailure=null`, desktop de entrada `Default` antes/depois. As capturas
`.local/initial-gate-preview.initial-gate-open.png` e
`.local/initial-gate-preview.npc-approach.png` foram geradas no desktop isolado.
O marcador é exigido pela bateria e pelo runner nos modos aplicáveis.

Próximo: perfil/draft limitado da alternativa Speech sobre os blocos separados,
com recusa de ramos não mapeados antes de qualquer mutação persistente.
Modelo recomendado: GPT-6.1 Sol, esforço Alto.
