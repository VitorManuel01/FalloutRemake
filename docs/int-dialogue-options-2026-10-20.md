# Referências literais de opções de diálogo

`IntDialogueOptions` reconhece `giq_option` (0x8121) quando os quatro argumentos
anteriores são pushes inteiros adjacentes: lista de mensagens, mensagem,
procedimento e reação. Conserva o requisito de inteligência quando é um push
literal ou literal seguido de negação. Uma expressão desconhecida permanece
desconhecida, sem inventar requisitos ou calcular fluxo.

Corpos compartilhados por procedimentos são percorridos uma vez e preservam
todos os índices proprietários. Padrões não atravessam limites físicos de
procedimento. Destinos fora da tabela são mantidos como número, com nome nulo.
Strings, argumentos calculados e padrões separados não recebem uma aresta
inventada. O resultado não prova que uma opção apareça ou seja escolhida.

`--script scripts/actemvil.int` agora inclui `DialogueOptionPatterns`.
Nos dados Epic conhecidos, os 53 `giq_option` físicos são reconhecidos:
nenhum opcode foi confundido com bytes de operando ou duplicado por aliases.
As quatro referências ao depósito em Node009 confirmam inteligência -3/4:
na regra CE, o requisito negativo significa inteligência efetiva até 3,
e o positivo significa pelo menos 4. Perk Smooth Talker participa da inteligência
efetiva no motor; a prévia ainda não implementa esse diálogo.

Validação: 235 grupos sintéticos, incluindo três grupos adicionais para sinais,
aliases, imutabilidade, IQ desconhecido, destino inválido, strings, expressões,
adjacência e limites de procedimento. Relatório real:
`.local/cameron-dialogue-patterns.json`. Nenhum script ou diálogo foi executado.

Próximo incremento: relatório específico do ramo Speech, com condições e
alterações de estado verificadas nas instruções e handlers CE.
Modelo recomendado: GPT-6.1 Sol, esforço Alto.
