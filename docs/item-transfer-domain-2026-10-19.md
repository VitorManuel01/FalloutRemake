# Marco 25 — fontes de itens e transferência conservadora

Branch `codex/temple-item-transfers`, base `c697a05`.

O Temple of Trials original contém sete recipientes raiz e sete registros de
conteúdo. Seis recipientes têm conteúdo, sem scripts próprios; o sétimo tem
script e permanece fora deste perfil. Os itens iniciais estão em recipientes,
não soltos. O pote ID 330/hex 19868 contém Healing Powder ID 106/PID 273;
aproximação alcançável em 70 passos após abrir a primeira porta. Ossos ID 40
contêm Spear ID 36/PID 7, aproximação em 133 passos nas mesmas condições.

`ItemTransferSession` mantém quantidades retiradas por chave fonte, separadas dos
registros imutáveis do MAP. Cada chave continua identificando seu recipiente,
mapa, ID, PID, FID e estado original. Retiradas parciais conservam a soma entre
conteúdo restante e carregado; repetir depois de esgotar não duplica itens.
Restauração é validada antes de substituir a sessão, sem inventar itens.

Perfil inicial: conteúdo imediato e sem filhos de um recipiente raiz, sem script
no item ou recipiente/protótipo, recipiente destrancado, personagem parado na
mesma elevação e mapa, no mesmo hex ou adjacente, fora de saídas e bloqueios.
Recusa flags desconhecidas no recipiente, itens ocultos/equipados, armas naturais,
recipientes aninhados, quantidades inválidas e fontes estrangeiras. Sem peso,
equipamento, uso/consumo, coleta de recipientes ou furto de criaturas.

Referência conferida localmente: Fallout 2 CE `e97087b`, `proto.cc:316`
(`_proto_action_can_pickup`), `proto_instance.cc:571` (`_obj_pickup`),
`proto_types.h` (`ITEM_HIDDEN`) e `obj_types.h` (`CONTAINER_FLAG_LOCKED/JAMMED`).
O perfil é uma etapa de teste e não reproduz todo o sistema de inventário do CE.

Validação: compilação sem avisos/erros e 126 grupos sintéticos aprovados. Quatro
novos grupos conferem conservação/IDs, proximidade/movimento/mapa/quantidades,
scripts/trancas/ocultos/armas naturais e restauração atômica. O relatório real
também percorre a rota até o pote, retira a unidade original, recusa repetição,
restaura numa sessão nova e verifica novamente os bytes do MAP.

```powershell
dotnet run --project tools/TempleInspector -- --data-dir 'D:\Games\fallout2\Fallout 2 English' --item-transfer-report
```

Relatório apenas local em `.local/item-transfer-report.json`. Este marco prepara
o domínio; a interface de coleta e o checkpoint são os incrementos seguintes.
Modelo recomendado: GPT-6.1 Sol, esforço Alto (High).
