# Marco 59 — continuação projetada até os recipientes da elevação 2

O relatório de efeito agora segue a navegação alternativa até a saída mais
próxima para a elevação 2. A saída conserva sua chave original e fornece chegada
ao hex 23914/elevação 2/rotação 0. A geometria original dessa elevação alcança
974 hexes a partir da chegada. Caminhos completos ficam no relatório para
reproduzir a aproximação, sem teleporte, passagem ou coleta na sessão viva.

A aproximação ao baú 376 é válida e as operações de consulta `CanTake` permitem
as duas unidades estáticas originais: Healing Powder ID 126/PID 273 e Antidote
ID 100/PID 49, uma unidade e peso base 1 cada. Não foi concedido inventário nem
somado peso à bolsa; são candidatos de uma projeção. As chaves de origem continuam
distinguindo o Antidote de outros objetos com ID numérico 100.

O baú 100/offset 465604 também é alcançável, mas permanece vazio e com estado
`ScriptRequired`. A análise não contorna o script do encontro com Cameron.
O checkpoint antes/depois da auditoria precisa ser idêntico; o relatório ainda
exige a reconstrução byte a byte do MAP. Nenhum asset foi extraído para o Git.

Validação: `--barrier-effect-report` passou sobre os dados Epic locais, com
assertivas para chegada, duas unidades esperadas, elegibilidade, peso e recusa
do recipiente com script. A implementação entrou no mesmo relatório da bateria.

Próximo: checkpoint de substituições e commit atômico de efeito/consumo; usar
esta continuação como critério de aceitação da passagem jogável. GPT-6.1 Sol,
esforço Alto.
