# Estado limitado de scripts, sem executar INT

`BoundedScriptState` mantém blocos explícitos de locais por instância, variáveis
do mapa e globais do jogo. Cada bloco tem chave, hash de origem, índices
permitidos e valores iniciais. Índices esparsos permitem liberar somente uma
global conhecida sem inventar o restante do estado do jogo.

Escritas são preparadas e publicadas numa transação com token/revisão. Índice
inválido, alvo desconhecido, espaço errado ou endereço duplicado recusam todo
o lote. Local 16 de um bloco 0–15 não pode alcançar a memória de outro script.
Captura, restauração e reset usam cópias próprias; restaurar/resetar invalida
operações antigas. Há limites de blocos, valores e escritas.

Cinco grupos adicionais cobrem separação, ausência de mutação parcial, índice
16, endereço esparso, duplicatas, token/revisão, no-op, schemas e snapshots
independentes e restauração atômica. **254 grupos sintéticos passaram** em
`.local/bounded-script-state-tests.log`.

Este marco não habilita o NPC, não interpreta INT e ainda não altera o checkpoint
do mundo v4. O schema do armazenamento é apenas uma base para um adaptador com
transições validadas; aceitar um inteiro num bloco não autoriza uma transição.

Próximo: construir o perfil a partir das identidades reais e conferir a global
original do desafio antes de persistir ou aplicar qualquer efeito.
Modelo recomendado: GPT-6.1 Sol, esforço Alto.
