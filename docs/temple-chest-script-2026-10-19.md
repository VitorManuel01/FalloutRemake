# Marco 36 — baú compartilhado por scripts

O baú vazio ID 100, offset 465604, hex 20132, elevação 2 do ARCAVES tem
SID 50331654 / índice 758. Não é uma fonte estática de itens para coletar.
No programa `aitemcst.int`, `map_update_p_proc` contém `self_obj` (2226),
push do identificador 454 (2228) e `store_external` (2234), publicando
`temple_chest_ptr`.

Em `actemvil.int`, Node009 contém a sequência física `dude_obj` (13346),
push do identificador 1044 (13348), `fetch_external` (13354) e
`move_obj_inven_to_obj` (13356). O handler do Community Engine move o inventário
do primeiro objeto para o segundo quando essa sequência é executada.
Isso não prova as condições de chegada ao nó de diálogo, nem a devolução posterior.
Nenhum script foi executado e nenhum adaptador novo foi habilitado.

O ator vinculado tem ID 126 e nome de protótipo Dumar. A mensagem 100 do recurso
de diálogo relacionado identifica Cameron. O relatório preserva essa distinção;
a prévia ainda apresenta o nome de protótipo, sem simular substituição por script.

## Leitor e reprodução

`IntProgram.IdentifierAt` valida offsets e terminadores dentro da tabela de
identificadores. `IntExternalReferences` reconhece apenas pushes imediatamente
anteriores a fetch/store/export; não infere fluxo nem duplica procedimentos
que compartilham o mesmo corpo. Tags 0x9001 e 0xC001 usam o operando como offset
de identificador nessas operações, conforme o handler CE, e não como string literal.

Execute localmente:

```powershell
dotnet run --project tools/TempleInspector -- --data-dir 'D:\Games\fallout2\Fallout 2 English' --temple-chest-report
```

O relatório exige os hashes conhecidos: MAP `BA35474B…B5BFE`, publicador
`D80BC3F2…1963C` e consumidor `D865DF15…D61F5`. Confere a reconstrução byte a
byte do MAP e descobre o recurso MSG pelos caminhos existentes. Dados e texto
original permanecem fora do Git. Referência local: fallout2-ce
`e97087b9582f37075db347a89898887320753f8b`, `interpreter.cc` (externals) e
`interpreter_extra.cc` (`self_obj`, `dude_obj`, `move_obj_inven_to_obj`).

Validação: compilação sem avisos/erros; 147 grupos sintéticos, incluindo limites
da tabela, operações com tags inteiras/string, aliases, adjacência e recusas;
relatório sobre os dados Epic reais. Próximo incremento seguro: informar o estado
de recipientes vazios que dependem de script. Para habilitar o baú jogável faltam
ciclo das variáveis externas, transição de diálogo/combate, propriedade dinâmica
dos itens e checkpoint compatível. Modelo recomendado: GPT-6.1 Sol, esforço Alto.
