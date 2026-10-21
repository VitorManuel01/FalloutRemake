# Entrada efetiva e recompensa única de Arroyo

Checkpoint **v8 / neutral-v8** guarda `Entries.EnteredMaps` separadamente das
áreas carregadas e um registro opcional `ArroyoReward`. Consultar uma passagem
pode carregar o destino, mas não registra uma entrada nem concede experiência.
Só a travessia explícita bem-sucedida atualiza destino, entrada e prêmio.

O adaptador recompensa somente o resultado de Speech verificado, na primeira
travessia do templo para **mapa 4/elevação 0/hex 20517/rotação 0**. Exige os
hashes originais de MAP, INT, GAM e scripts.lst e confere a guarda de primeira
entrada e o ramo G10=2. Concede **600 XP**, sem alterar G10: o controle de
repetição pertence ao histórico de entradas. O perfil neutro não tem perks;
a concessão é recusada se pudesse alcançar o primeiro limiar de nível.

No fluxo real, os 25 XP de arrombamento tornam-se **625 XP** ao atravessar.
Itens, estado do NPC, porta aberta, bytes originais e navegação são conservados.
Retornar à área ou carregar o checkpoint não repete o prêmio. Falha na
verificação do destino/perfil mantém mapa ativo, posição, entradas e XP.
O cache de destino pode existir antes disso, conforme o comportamento anterior.

Arquivos v1–v7 migram de forma conservadora: todas as áreas já presentes no
arquivo são consideradas visitadas, pois não há como provar se eram apenas
cache. Arroyo já presente não recebe prêmio retroativo. Se Arroyo nunca foi
carregado nesse arquivo, a primeira entrada elegível continua podendo recebê-lo.
v8 preserva a distinção exata, incluindo salvar antes de entrar com o destino
carregado. Reiniciar a prévia limpa prêmio e entradas, mas não apaga o slot.

Restauração valida mapas presentes, duplicatas, área inicial/atual, valor fixo
do prêmio, perfil original e resultado da conversa. JSON v8 exige os campos
de entrada; premiações sem conversa ou com quantias/identidades alteradas são
recusadas antes da troca da sessão. Arrays devolvidos são cópias.

Validação: **316 grupos sintéticos**, **19/19 etapas**, builds sem avisos/erros,
manifesto `.local/temple-validation-20261021-110813-0f948c0e/manifest.json`.
`ARROYO_ENTRY_REWARD_OK` comprova a entrada real, 25→625 XP e restauração.
Os testes também cobrem consulta, destino em cache, retorno, nova entrada,
save antes/depois, reset, migração antiga e falhas/arquivos adulterados.
Captura isolada 1280×800 aprovada em `.local/arroyo-reward-isolated.json`,
desktop de entrada `Default` preservado. O HUD mostra **XP 625** na vila.

Filmes, reputação, outros ramos de recompensa, perks e evolução de nível ainda
não são executados. O [relatório de evidência](arroyo-entry-reward-audit-2026-10-21.md)
continua disponível; agora também comprova a criação do perfil do runtime.

Próximo marco solicitado: árvores Nature no exterior, preservando bloqueadores
e IDs. Modelo recomendado: **GPT-6.1 Sol, esforço Alto**.
