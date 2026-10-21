# Estado Speech e saída do NPC no mundo — checkpoint v6

O mundo passa a guardar a aceitação Speech, o local 14, a pose do NPC, a fase,
a espera e o estado derivado da porta em um agregado. Posição e bloqueador são
projetados pela chave original. O MAP permanece imutável.

Preparar a aceitação não muda a partida. Confirmar exige o perfil conferido,
INT 5/Speech marcado, cinco escolhas aceitas, proximidade e repouso. Token,
revisão e hash do mundo recusam propostas antigas, inclusive após mudança de
porta, posição, reset ou carregamento. Uma porta já aberta permanece aberta.
A porta fica sob controle da sequência enquanto esse estado estiver ativo.

Passos manuais revalidam o próximo hex contra os bloqueadores atuais e o jogador.
Chegar à porta agenda 20 ticks; avançar a espera não move o NPC. O relógio do
explosivo não altera este evento. Fora do mapa 3/elevação 2, ações da sequência
ficam indisponíveis, conservando o estado até o retorno.

O checkpoint v6 reúne esse agregado ao restante da partida. A restauração exige
um resolvedor do perfil original, valida variáveis/fase/porta antes de substituir
o mundo e cria tokens novos. Formatos v1–v5 continuam legíveis e não recebem
aceitação ou movimento inventados. JSON duplicado é recusado; v6 exige o campo
Temple explícito, inclusive quando nulo.

Validação do domínio: **294 grupos sintéticos** (+9), incluindo migração de
todos os cinco formatos anteriores, rollback, ocupação, replay de ações,
restauração após cada passo/tick, posse dos arrays e JSON estrito. Build do
inspetor sem avisos/erros. A interface ativa é o próximo incremento.
