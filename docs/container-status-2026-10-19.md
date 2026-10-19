# Marco 37 — estado do recipiente vazio

A janela J agora informa quando um recipiente sem conteúdo original depende de
uma interação ainda não disponível. O baú ID 100, offset 465604, elevação 2,
mostra esse aviso em vez de uma mensagem genérica de inventário vazio.
Um recipiente comum sem conteúdo aparece como vazio. Pilhas já recolhidas
continuam com sua própria mensagem de esgotamento.

`ItemTransferSession.ContainerStatus` consulta o proprietário independentemente
dos itens: scripts de objeto/protótipo, posição/elevação, ocultação, tranca e
layout de protótipo. A transferência usa as mesmas verificações e mantém a
recusa de itens inexistentes. A consulta não executa scripts nem modifica o MAP.

Validação: compilação sem erros/avisos; 148 grupos sintéticos aprovados, incluindo
recipientes vazios comuns, com script, trancados, ocultos e sem protótipo.
A integração Godot verifica o baú original, ações desabilitadas e conservação
de personagem, bolsa, portas e bytes fonte (`SCRIPTED_CONTAINER_OK`).
As demais verificações de coleta, devolução, checkpoint e quantidades passaram.
Captura pelo runner isolado, sem janela no desktop ativo.

Próximo: estudar a devolução do inventário na sequência de diálogo do templo,
antes de decidir um adaptador jogável. GPT-6.1 Sol, esforço Alto.
