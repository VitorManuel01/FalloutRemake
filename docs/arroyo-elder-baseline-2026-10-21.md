# Base estática para o próximo diálogo de Arroyo

O candidato original foi localizado no mapa 4: anciã com **PID 16777229**,
FID **16777247**, hex **18915**, elevação **0**, SID **67108867** e índice de
script **10**. A identidade de origem é o offset **126588**, chave
`0EDCDFF2AFB6FAC7E8203CE9EAE8BA4663D37F3BE112D3EF4713AF3093D8D52A:126588`.
O ID 20 isoladamente não basta: outro NPC desse mapa também usa ID 20.

Programa original: `scripts/ahelder.int`, **20658 bytes**, SHA256
`7B33E32FF41BC24504658C300D3ED8C068D6F1621CAC02F697321EE204A8D27F`.
O leitor existente identifica **4592 instruções**, **53 procedimentos**,
**76 padrões de opções de diálogo**, **162 escritas constantes** e **199
leituras literais de variáveis**. Pontos encontrados: `start` em **4466**,
`talk_p_proc` em **4734**, `map_enter_p_proc` em **11604**.

Isso é inspeção estrutural, não prova de controle de fluxo ou autorização para
executar todos os padrões. Há leituras de locais 0–5 e 7–11 e de diversos
globais, incluindo 37–44 e 47. Esses índices também aparecem na inicialização
original da vila, que o adaptador atual de recompensa não executa integralmente.
A interseção sugere uma dependência a investigar; ainda é necessário provar
quais caminhos são alcançados no primeiro diálogo.

Antes de uma conversa ativa adicional: conferir a declaração de locais na
entrada 10 de scripts.lst, mapear guardas e mensagens iniciais, auditar os
globais necessários da primeira entrada e isolar um único ramo verificável.
Não reutilizar o bloco de locais do NPC do templo ou presumir que G10=2
determina sozinho o diálogo da anciã.

Evidência somente local: `.local/arroyo-elder-static-report.json`, gerada pelo
comando já existente `--script scripts/ahelder.int --instructions`. Nenhuma
conversa, variável, recompensa ou asset foi alterado por essa inspeção.
Próximo marco recomendado: auditar o estado de entrada da vila exigido pelo
ramo inicial da anciã. **GPT-6.1 Sol, esforço Alto**.
