# Retirar tudo na janela J

O novo botão **Retirar tudo** fica junto das ações por unidade/pilha/quantidade.
A disponibilidade e a dica refletem a capacidade somada, a proximidade e a
elegibilidade de todo o recipiente. Retirar uma pilha ainda pode ser permitido
quando o conjunto completo não cabe. Não há coleta parcial automática.

No clique, a operação valida novamente o mundo atual. A solicitação pertence à
janela, recipiente e sessão que a criaram; fechar ou recriar o diálogo invalida
callbacks anteriores. Depois da coleta, quantidade, peso, botão e representação
do recipiente são atualizados. Recipientes especiais continuam bloqueados.

`TAKE_ALL_UI_OK` atravessa a passagem aplicada e coleta os dois itens originais
do baú superior: powder ID 126/PID 273 e antidote ID 100/PID 49. Confere duas
unidades, 2 lb e um único avanço de revisão. Recusa repetição e testa callback
antigo com conteúdo novamente coletável, devolvendo uma unidade para montar
essa condição; somente o callback da janela atual pode recolhê-la.
O ensaio existente valida em seguida checkpoint, bolsa e retorno de elevação.

Componentes também cobrem botão bloqueado por carga agregada e peso desconhecido,
mesmo com retirada individual habilitada. O marcador passou a ser exigido tanto
pela bateria headless quanto pelo runner gráfico nos modos que exercitam a ação.

Validações: 232 grupos sintéticos no marco de domínio; bateria de 14/14 etapas
em `.local/temple-validation-20261020-195654-02314a06/manifest.json`;
reforço do teste de callback em `.local/take-all-refined.log`, sem erros do motor.
Captura 1024×768 inspecionada em `.local/take-all-ui.take-all-before.png`.
Runner `.local/take-all-ui-isolated.json`: passou, desktop `Default` preservado.

Próximo marco: extrair referências literais de opções de diálogo dos INT para
mapear a alternativa ao combate sem executar scripts ou inventar condições.
Modelo recomendado: GPT-6.1 Sol, esforço Alto.
