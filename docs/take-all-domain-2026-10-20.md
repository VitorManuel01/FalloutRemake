# Retirar todas as pilhas sem transferência parcial

`WorldSession.CanTakeAllItems` valida o recipiente, cada item restante,
proximidade, movimento, saídas e o peso somado de todas as pilhas. A carga inclui
também os itens de áreas inativas. Peso desconhecido ou carga excessiva recusam
a operação inteira, mesmo quando uma pilha isolada caberia na bolsa.

`TakeAllItems` revalida no momento da chamada. A sessão prepara um dicionário
completo de propriedade antes de publicá-lo numa única substituição; a revisão
avança uma vez. Não modifica MAP, chaves fonte, unidades posicionadas/consumidas,
itens já carregados ou origem. Não habilita recipientes com script. Não requer
novo formato de checkpoint: as retiradas são registros `TakenItem` já suportados.

Cinco grupos sintéticos adicionais cobrem limite exato de carga, duas pilhas do
mesmo PID com chaves distintas, coleta anterior parcial, repetição, restauração,
devolução à origem, excesso agregado, item incompatível, tranca, movimento,
distância, peso desconhecido e carga de área inativa após checkpoint.

Próximo incremento: botão **Retirar tudo** na janela J, com estados de recusa,
revalidação no clique e teste sobre as duas pilhas reais do baú superior.
Modelo recomendado: GPT-6.1 Sol, esforço Alto.
