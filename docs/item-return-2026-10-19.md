# Marco 29 — devolver ao recipiente de origem

Branch `codex/return-container-items`, base `8ba4ab3`.

A bolsa B oferece **Devolver 1** e **Devolver esta pilha**. Exige que o personagem
esteja parado na mesma área/elevação e ao alcance do recipiente original. Não
transfere para outro proprietário, não joga no chão e não usa/equipa o item.
Quando estiver longe ou em outra área, mostra a orientação e desativa a ação.

A devolução reduz a quantidade carregada na mesma chave e repõe exatamente essa
quantidade no recipiente. Ao devolver tudo, remove a pilha da bolsa; o item pode
ser recolhido novamente com J. Repetir não cria unidades. Fontes e IDs não mudam.
Checkpoint v2 já guarda a quantidade líquida retirada, portanto não houve nova
migração de formato. Uma carga posterior reproduz o restante de ambos os lados.

Validação: compilação sem avisos/erros, **134 grupos sintéticos aprovados**.
Quatro grupos novos: retiradas/devoluções repetidas e parciais, recusa de
distância/movimento/origem/quantidade inválida, interações em saída recusadas e
checkpoint com quantidade líquida. Sessão e fontes permanecem íntegras nas falhas.

Godot sem janela e gráfico isolado com saída 0, `ITEM_RETURN_OK`: Healing Powder
ID 106, botões 1/pilha, recuperação do conteúdo na janela J, recusa de repetição,
quantidade excessiva, movimento, distância e devolução remota desde o exterior.
Retorno ao templo e salvar/reiniciar/carregar confirmam bolsa vazia e recipiente
com a unidade original. Ator, portas e reconstrução do MAP conferidos.

Janela B ajustada para comportar os controles, com limites conferidos em 1440 ×
900. Captura `.local/temple-return.return.png`; logs `.local/return-build.log`,
`.local/return-synthetic.log`, `.local/return-preview.log` e resultado isolado.
Sem janela no desktop ativo ou dados proprietários no Git.

Próximo: apontar/clicar em recipientes no modo Caminhar para prever e solicitar
a mesma aproximação disponível na janela J. GPT-6.1 Sol, esforço Alto (High).
