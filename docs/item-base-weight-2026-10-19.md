# Marco 32 — peso base original dos itens

Branch `codex/item-base-weight`, base `e166e0b`.

O leitor PRO expõe `BaseWeight`, inteiro big-endian no offset 44 dos sete tipos
de item. Objetos de outros tipos não têm esse campo. Valores zero são preservados;
pesos negativos são recusados pelo perfil de dados padrão. O relatório de
transferências mostra o peso base do recipiente e de cada item sem mudar bytes.

Referência local conferida: CE `proto.cc`, leitura de PRO de itens a partir da
linha 1664, e `item.cc:757`, `itemGetWeight`. O peso base ainda não inclui munição
carregada, conteúdo de recipientes ou ajustes de armadura. A contagem de carga
será uma regra separada; este marco não altera a capacidade nem o checkpoint.

Dados reais do templo: Spear 4 lb, Healing Powder 1 lb, Antidote 1 lb e Plastic
Explosives 4 lb. Os sete registros de conteúdo totalizam 13 lb de peso base.

Validação: compilação sem avisos/erros, **137 grupos sintéticos aprovados**.
Três grupos novos conferem os sete layouts, offset independente de tamanho/
material/custo, zero/máximo inteiro, rejeição de negativo e ausência do campo em
paredes. Relatório real com retirada/restauração e reconstrução do MAP aprovados.
Fontes e resultados em `.local/base-weight-*`, fora do Git.

Próximo: peso efetivo dos itens suportados e limite de carga do perfil de teste.
GPT-6.1 Sol, esforço Alto (High).
