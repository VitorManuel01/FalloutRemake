# Marco 39 — voltar pela passagem indicada na bolsa

Quando o recipiente fica em outra área ou elevação visitada e há caminho
disponível, B informa passos e passagens e oferece **Ir à passagem**.
O botão fecha a bolsa, volta à elevação real do personagem e caminha somente
até a próxima saída. Ao chegar, **Ver passagem** mostra a saída sem atravessar.
O jogador usa Enter e abre B novamente para continuar. Perto do recipiente,
a devolução permanece uma ação explícita.

O plano é recalculado ao acionar o botão; não usa uma rota antiga armazenada
na janela. Sem caminho, enquanto caminha ou sem a origem ainda na bolsa,
a ação é recusada. Áreas desconhecidas não são carregadas por essa consulta.
Portas não se abrem automaticamente. Não há alteração de schema de checkpoint.

Validação Godot sem janela: coleta do pó original ID 106, inspeção em elevação
diferente, aproximação local, ida ao exterior, percurso até a passagem,
recusa de viagem/devolução automáticas, Enter, retorno ao pote e devolução
explícita da mesma unidade. Quantidades, portas e MAPs continuam preservados.
`BAG_ORIGIN_OK` agora inclui essa sequência completa. As outras integrações
de bolsa, checkpoint, carga e quantidades passaram.

Captura pelo runner em desktop isolado, sem interferir no desktop ativo.
O relatório CLI do marco 38 mantém a reprodução dos 72 passos/uma passagem
do caso original. Modelo para os próximos incrementos: GPT-6.1 Sol, esforço Alto.
