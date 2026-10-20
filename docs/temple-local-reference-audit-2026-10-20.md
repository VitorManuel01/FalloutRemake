# Auditoria de referências aos locais declarados

O extrator `IntVariableReads` reconhece getters de local do script, variável do
mapa e global do jogo precedidos por índice inteiro literal. Conserva aliases,
offsets e valores inválidos como evidência. Não lê estado, executa script,
atravessa corpos físicos ou estima índices calculados.

Nos recursos Epic resolvidos, `actemvil.int` referencia local **16** em **14504**
e **14528**, ambos no corpo Node012a. `scripts.lst` declara **16 entradas** para
esse programa; portanto os dois getters ficam fora de 0–15. Para `aitemdor.int`,
nenhum getter literal identificado ficou fora das 9 entradas declaradas.

O relatório `--temple-dialogue-report` apresenta essas referências e sinaliza
investigação necessária. Não aumenta contagens automaticamente, muda os dados
originais ou habilita o NPC. A análise não cobre todos os setters/índices
calculados e não prova execução do ramo, alias de memória ou falha no jogo.

Três grupos adicionais verificam espaços, aliases, imutabilidade, índices
negativos, expressões, strings, globais internas e fronteiras de procedimentos.
**249 grupos sintéticos passaram**. Relatório real:
`.local/temple-local-reference-audit.json`, com reconstrução original verificada.

Próximo marco do NPC: esclarecer a origem dessa divergência na lista do patch
e a semântica de armazenamento do CE, antes de definir o estado persistente.
O ramo Speech conhecido permanece apenas analisado; coleta e devolução de
recipientes estáticos continuam independentes dessa questão.
Modelo recomendado: GPT-6.1 Sol, esforço Alto.
