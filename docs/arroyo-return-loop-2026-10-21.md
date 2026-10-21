# Volta ao templo e segunda chegada à vila

O painel da partida oferece **Voltar ao templo** em Arroyo após a conversa
aceita. Ele escolhe uma passagem original alcançável e solicita a caminhada;
**Enter** continua confirmando a travessia. A consulta não carrega outra área,
altera XP ou move o personagem. Caminhada em curso e saídas com destinos
conflitantes são recusadas. Os arrays de consulta pertencem ao chamador.

A integração usa o percurso real da vila até um dos marcadores de volta a
**mapa 3/elevação 2/hex 14128/rotação 0**. No templo, a porta 100 continua aberta
e o NPC permanece oculto. Um save nesse retorno conserva o prêmio já recebido.
Uma segunda caminhada pela saída original da vila mantém **625 XP** e os dois
itens do cenário de teste, sem repetir os 600 XP.

Também é tentado carregar um checkpoint adulterado para conceder 601 XP em vez
dos 600 permitidos. A sessão ativa e seus bytes capturados são preservados
integralmente quando esse arquivo é recusado, e o save correto é regravado.

Testes sintéticos adicionais cobrem consulta somente leitura, cópias de rota,
destino em cache, movimento e ambiguidade. `ARROYO_RETURN_LOOP_OK` identifica
o percurso nativo completo, o checkpoint no retorno e a recusa do prêmio
adulterado. Nenhum asset proprietário ou arquivo original foi alterado.
Bateria completa: **319 grupos sintéticos**, **19/19 etapas**, builds sem
avisos/erros, `.local/temple-validation-20261021-111543-c7e881aa/manifest.json`.

Próximo incremento: tornar a seleção das malhas Nature coerente também na
parte alta dos troncos, mantendo identidade e navegação do mapa original.
Modelo recomendado: **GPT-6.1 Sol, esforço Alto**.
