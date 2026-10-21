# Preferência visual Nature persistente

**Nature 3D** passa a ser salva junto das opções de grade, piso e revelação do
personagem. O arquivo local de preferências usa versão **2**, com booleano
explícito `ShowNatureModels`. A preferência é aplicada antes de carregar o mapa
e não faz parte do checkpoint da partida.

A versão 1 continua sendo lida com Nature habilitado por padrão, mantendo
as três opções anteriores. A leitura não regrava o arquivo. A próxima escolha
explícita salva versão 2 de forma atômica; um erro de gravação conserva o
arquivo anterior e mantém a opção escolhida na sessão atual. JSON incompleto,
tipos incorretos, propriedades repetidas e campos desconhecidos são recusados.

Os testes cobrem as 16 combinações de opções, leitura de v1 sem escrita,
validação estrita de v2, preservação do arquivo em falha e limpeza de arquivos
temporários. Os testes de interface usam exclusivamente o arquivo isolado de
teste e conferem botão, cena, reconstrução e K/R sem alterações na partida.
Bateria aprovada: **321 grupos sintéticos**, **19/19 etapas**, builds sem
avisos/erros em `.local/temple-validation-20261021-112530-69c347f0/manifest.json`.

Modelo recomendado para continuidade: **GPT-6.1 Sol, esforço Alto**.
