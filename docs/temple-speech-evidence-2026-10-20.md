# Alternativa Speech: condições e efeitos identificados

`--temple-dialogue-report` verifica MAP e `actemvil.int` Epic por hash, reconstrói
o MAP e confere os operandos dos trechos descritos abaixo. Não executa script,
rolagem, diálogo, movimento do NPC ou mudança de estado.

O ator fonte é ID 126, offset 425776, SID 67108869. O protótipo se chama Dumar;
o diálogo associado identifica Cameron. A porta controlada é ID 104, offset
462744, hex 19928, elevação 2, PID 33555366, ligada a `aitemdor.int`.
Não confundir essa porta com o baú do inventário.

Sequência parcial comprovada:

- Node003 só registra a opção de mensagem 165 → Node014 se
  `metarule(40, 14)` for verdadeiro: o handler CE consulta se **Speech é tagged**.
  Essa opção também exige inteligência efetiva pelo menos 4.
- Node014 tem a opção 168 → Node015, também para inteligência efetiva >= 4.
- Node015 chama `roll_vs_skill(player, Speech=14, modificador=0)` em 14240,
  guarda o resultado no slot de frame 0 e testa **Speech tagged OU sucesso da
  rolagem**. `opSuccess` reconhece sucesso normal/crítico; `opIf` salta quando
  o resultado está vazio/falso. Portanto não exigir um sucesso aleatório extra
  quando tagged já torna a condição verdadeira.
- O lado aceito oferece 171 → Node015a. O outro oferece 172 → Node002.
- Node015a grava locais do NPC 12=1 e 13=2, variável do mapa 20=2 e global do
  jogo 10=2. Não é o mesmo espaço de variáveis para todos esses índices.
- Em `critter_p_proc`, local 13=2, local 14=0 e NPC fora do hex 19728 conduzem
  ao lookup da porta e `obj_unlock` em 4494. Existe movimentação/fase posterior;
  esse trecho não significa abrir a porta imediatamente no clique de diálogo.
- Os corpos físicos Node014, Node015 e Node015a não contêm transferência completa
  de inventário. O depósito do desafio está em Node009, outro ramo.

Referência local CE `e97087b9582f37075db347a89898887320753f8b`:
`src/skill_defs.h`, `src/interpreter.h`, `src/interpreter.cc` (`opIf`) e
`src/interpreter_extra.cc` (`opRollVsSkill`, `opSuccess`, `opMetarule`, setters).

Faltam validar inicialização de locais/entrada da conversa, perfil de inteligência
efetiva e tags, ciclo do NPC/porta e persistência de variáveis de mapa/globais.
Logo, é evidência de uma alternativa ao combate, sem afirmar que a sequência
completa esteja jogável. Nenhum adaptador foi habilitado e nenhum texto original
de diálogo foi copiado para o Git.

Relatório real: `.local/temple-dialogue-evidence.json`, com 53 opções literais.
A bateria passou a incluir esse relatório e possui **15 etapas**. Os testes do
leitor somam 235 grupos sintéticos nesta base.
Resultado: **15/15 aprovadas**, manifesto
`.local/temple-validation-20261020-200202-a9c4ad2e/manifest.json`.

Próximo passo para esse ramo: validar a porta `aitemdor` e a inicialização dos
estados antes de implementar uma transição persistente.
Modelo recomendado: GPT-6.1 Sol, esforço Alto.
