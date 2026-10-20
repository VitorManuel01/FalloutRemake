# Baú do templo: entrada do inventário e recuperação ainda não comprovada

Marco concluído na branch `codex/temple-chest-return-analysis`, sobre `dc3962a`.
Análise estática dos mesmos MAP/INT Epic e hashes registrados no marco 36.
Nenhum script foi executado e nenhuma interação nova foi habilitada.

O relatório `--temple-chest-report` agora verifica também:

- `actemvil.int` contém uma única instrução física `move_obj_inven_to_obj`, em
  13356. Os argumentos imediatamente anteriores são jogador e `temple_chest_ptr`.
  O handler CE retira destino e origem da pilha e chama `itemMoveAll(origem, destino)`.
- `aitemcst.int` não contém essa instrução. Nos dois programas examinados não há
  outra transferência completa que comprove a devolução automática. Isso não
  exclui recuperação manual, operações individuais ou efeitos de outros scripts.
- Quatro opções físicas `giq_option` referenciam o índice 28 (`Node009`):
  Node006 em 12966/13032, mensagens 134/136; Node008 em 13214/13280, mensagens
  141/143, todas da lista 749. O relatório valida os operandos adjacentes.
  O CE condiciona a apresentação por inteligência e registra um destino de
  diálogo. Essas referências não provam chegada ao nó, escolha ou sucesso no desafio.
- Depois da transferência, o lookup em 13376 usa hex 19928, elevação 2 e
  PID 33555366. O objeto encontrado é armazenado e reutilizado para fechar
  (13394) e trancar (13404). É distinto do baú PID 245 no hex 20132.
  Não interpretar essas operações como trancar o baú do inventário.
- O publicador possui apenas os registros auxiliares, `start` e
  `map_update_p_proc`; não possui `use_p_proc` ou `pickup_p_proc`.
  A ausência desses procedimentos não prova que o recipiente seja inacessível:
  o CE também tem tratamento padrão de recipientes em `_obj_use_container`.

A contagem percorre instruções decodificadas uma vez, evitando duplicar corpos
compartilhados por aliases. O escopo é limitado aos dois programas conhecidos,
sem interpretação geral do fluxo, execução da VM ou busca global de scripts.

Referência local: fallout2-ce `e97087b9582f37075db347a89898887320753f8b`,
`src/interpreter_extra.cc`: `_op_giq_option`, `opMoveObjectInventoryToObject`,
`opObjectClose`, `opObjectLock`; `src/proto_instance.cc`: `_obj_use_container`.
O repositório mantém apenas o analisador e a descrição; dumps permanecem em `.local/`.

Validação: bateria completa headless, incluindo relatório sobre os dados reais,
builds, testes sintéticos e integração Godot. O resultado exato consta no
[resumo consolidado](milestones-through-2026-10-20.md).

Próximo passo, quando solicitado: comprovar a recuperação manual do baú e os
estados do desafio, antes de projetar custódia dinâmica dos itens e checkpoint.
Modelo recomendado: GPT-6.1 Sol, esforço Alto. O baú permanece especial e vazio
na prévia; não recebe itens inventados nem uma ação de devolução presumida.
