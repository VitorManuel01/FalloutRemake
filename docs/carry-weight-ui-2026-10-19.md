# Marco 34 — pesos e carga nas janelas

Branch `codex/carry-weight-ui`, base `5c7ea7c`.

J mostra o peso da quantidade restante em cada pilha e a carga atual/capacidade.
B mostra peso por pilha e carga total, incluindo itens de áreas inativas; informa
Força 5 como perfil de teste. Ao retirar ou devolver, os números são atualizados.
Pilha esgotada mostra 0 lb. Peso sem regra suportada aparece como indisponível.

Retirar 1 pode permanecer disponível quando a pilha completa ultrapassa a carga.
Nesse caso, o botão da pilha fica desativado e explica o motivo no tooltip. Os
botões consultam a mesma regra de capacidade da transferência real.

Validação: compilação sem avisos/erros; integração completa sem janela e gráfica
isolada com saída 0. `CONTAINER_LOOT_OK` confere 1 lb antes/depois da transferência
e `PLAYER_BAG_OK` confere carga 1/150 lb no exterior após checkpoint. Novo
`CARRY_WEIGHT_UI_OK` testa os estados de capacidade excedida, unidade versus
pilha, tooltip e peso desconhecido com fixtures de componente, sem transferir
itens reais. Os 144 grupos sintéticos do domínio/persistência seguem aprovados.

Capturas `.local/temple-weight-ui.loot.png` e `.bag.png` revisadas, 1440 × 900.
Logs `.local/weight-ui-build.log`, `.local/weight-ui-preview.log`, stdout/stderr
e JSON da execução isolada. Sem janela no desktop ativo ou ativos no Git.

Próximo sugerido: quantidade explícita na transferência de pilhas, mantendo o
mesmo limite de carga e os IDs fonte. GPT-6.1 Sol, esforço Alto (High).
