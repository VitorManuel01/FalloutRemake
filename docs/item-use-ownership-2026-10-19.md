# Marco 50 — conservação após posicionar ou consumir

`ItemTransferSession` separa a quantidade retirada da origem da quantidade ainda
carregada. Alocações com GUID próprio representam uma unidade posicionada ou
consumida, mantendo as chaves de proprietário/item e o registro fonte intacto.
O balanço é original = restante + carregada + posicionada + consumida.
Devolver aceita somente a parte ainda carregada; usar não repõe a origem.

O perfil atual aceita apenas o explosivo original pelo hash PRO já verificado.
As alocações retêm local, prazo e estado, sem ativar relógio, dano ou objetos
visuais. As operações de posicionar/consumir são internas e não têm botão na
prévia. GUID duplicado, consumo repetido, quantidade inventada, fonte modificada
e registros incompatíveis são recusados sem alterar estado ou revisão.

A restauração prepara e valida todas as quantidades/alocações antes de substituir
a sessão. O limite é 1024 alocações por área. Snapshot de revisão invalida planos
antigos em um próximo commit de uso; essa revisão não modifica o MAP.

Validação: build sem avisos/erros e 174 grupos sintéticos aprovados. Novos testes
cobrem pilha parcial, devolução após uso, reset, mistura de posicionados/consumidos,
duplicações, snapshots independentes, limites e restauração atômica.

Neste commit intermediário, checkpoint v2 recusa alocações não vazias em vez de
perdê-las ou recriá-las na bolsa. A prévia continua sem criar alocações. O próximo
marco adicionará schema v3 e leitura compatível de v1/v2 antes de ligar uso ao mundo.
Modelo recomendado: GPT-6.1 Sol, esforço Alto.
