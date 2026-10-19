# Marco 26 — itens no mundo e checkpoint

Branch `codex/item-checkpoints`, base `6134b72`.

Cada área carregada mantém sua sessão de retiradas. A bolsa do personagem é
derivada dessas sessões, com mapa, recipiente, chave do objeto original e
quantidade; trocar de área não duplica nem descarta itens. Reiniciar a prévia
restaura recipientes e esvazia a bolsa, preservando o slot salvo.

Novos checkpoints são versão 2 / `neutral-v2`, com lista `Taken` em cada área.
Gravam somente chave do recipiente, chave do item e quantidade retirada. IDs,
PID, FID e estado vêm novamente da fonte validada pelos hashes. Um checkpoint
versão 1 / `neutral-v1` continua carregando, com bolsa vazia; não aceita dados de
itens injetados sob o esquema antigo. Versão 2 exige listas explícitas, inclusive
vazias. Salvar depois de carregar uma versão 1 produz a versão 2.

Restauração usa um mundo candidato: quantidades, chaves, elegibilidade, hashes,
portas e posições precisam ser válidos antes de substituir a campanha ativa.
Recusa itens estrangeiros, duplicações e retiradas além da quantidade original.
As preferências visuais e o arquivo MAP permanecem separados.

Validação: compilação sem avisos/erros, **130 grupos sintéticos aprovados**.
Quatro grupos novos cobrem inventário em mundo novo, viagem/retorno/reset,
migração de versão 1 e rejeição atômica de dados forjados.

Integração Godot sem janela com saída 0 e `ITEM_CHECKPOINT_OK`: primeira porta,
rota real ao pote 330, Healing Powder ID 106/PID 273, retirar/salvar/reiniciar/
carregar, checkpoint no exterior com fonte em área inativa, retorno sem nova
unidade e tentativa de forjar quantidade 2 recusada mantendo a campanha atual.
Reconstrução dos MAPs verificada. Apenas `preview-test.json` foi usado.

Logs `.local/item-checkpoint-{build,synthetic,preview}.log`. O domínio está
integrado e validado; o próximo marco oferece a transferência na interface.
Modelo recomendado: GPT-6.1 Sol, esforço Alto (High).
