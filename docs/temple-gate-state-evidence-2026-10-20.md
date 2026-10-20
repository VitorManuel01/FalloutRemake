# Porta final: valores do MAP e ciclo do script

O leitor agora também identifica setters com índice/valor inteiros adjacentes:
`set_local_var`, `set_map_var` e `set_global_var`. Mantém os espaços distintos,
não confunde com globais internas do programa e não transforma padrões físicos
em execução comprovada. O relatório `--script` expõe as escritas constantes.

Três grupos sintéticos adicionais cobrem espaços, aliases, fonte imutável,
valor negativo, strings, expressões, opcodes de outros espaços e limites físicos
de procedimento. **242 grupos passaram**. O relatório Speech utiliza o extrator
para conferir as quatro escritas de Node015a, com os offsets originais.

O relatório do templo também lê e verifica `aitemdor.int` pelo hash conhecido:

- Na primeira entrada, quando local 3=0, grava locais 3=1, 1=1 e 0=1.
- A exceção que zera local 0 exige **hex 13528**, variável do mapa 20 diferente
  de 2 e local 7=0. Não corresponde à porta final do hex 19928.
- Depois, local 0=0 tranca; outro valor destranca.
- Em `map_update`, variável do mapa 20=1 grava local 0=0, levando à tranca.
- O MAP fonte tem variável 20=0 e openFlags da porta final zero. Sob locais
  iniciais zerados, a primeira entrada dessa instância a deixa destrancada;
  isso não permite ignorar as mudanças posteriores do desafio.

Esses resultados recusam a suposição de que toda instância do mesmo PID começa
trancada ou segue o mesmo ramo. Não habilitam abertura, arrombamento ou chave da
porta final. O ciclo com NPC, locais, variável 20 e global do jogo permanece
fora da sessão atual. A inferência condicionada não é uma execução da VM.

Referência CE local: `scripts.cc`, `interpreter.cc`, `interpreter_extra.cc`;
hash do programa `3E8AE39C72E0347330FF967EE7DEE36DBBA31CA27087F26D037A08F5E6C1B9DB`.
Evidência: `.local/temple-gate-evidence.json`, com reconstrução do MAP verificada.

Próximo marco: conferir `local_vars` em scripts.lst e a alocação inicial do CE,
para substituir a premissa de locais zerados por evidência da instalação.
Modelo recomendado: GPT-6.1 Sol, esforço Alto.
