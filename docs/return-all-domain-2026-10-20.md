# Devolver todas as pilhas ao mesmo recipiente de origem

`CanReturnAllItems`/`ReturnAllItems` atuam sobre uma área e um recipiente de
origem explícitos. Não esvaziam a bolsa inteira nem transferem entre recipientes.
Verificam elegibilidade, posição, movimento e saída antes de preparar/publicar
o novo conjunto de retiradas numa única revisão.

Somente a quantidade carregada é devolvida. Unidades posicionadas e consumidas
permanecem alocadas e descontadas da origem; não ressurgem no recipiente.
Itens de outras origens permanecem na bolsa. A operação também permite reduzir
uma bolsa antiga com peso desconhecido. Não há limite de carga para devolver.
Recipientes com script continuam recusados, assim como devolução remota.

Quatro grupos adicionais verificam duas pilhas e outra origem, uma revisão,
repetição, checkpoint, distância, movimento, outra área, saída, usos mistos e
peso desconhecido. **239 grupos sintéticos passaram** em
`.local/return-all-domain-tests.log`. O formato de checkpoint permanece v4.

Próximo incremento: ação na bolsa para devolver conjuntamente os itens da
origem selecionada, com proteção de callbacks e validação na cena real.
Modelo recomendado: GPT-6.1 Sol, esforço Alto.
