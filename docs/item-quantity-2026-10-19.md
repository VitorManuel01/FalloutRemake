# Marco 35 — quantidade escolhida na transferência

Branch `codex/item-quantity-controls`, base `4257500`.

Pilhas com mais de uma unidade mostram Qtd. e **Retirar quantidade** ou
**Devolver quantidade** nas janelas J/B. O seletor aceita inteiros de 1 até a
quantidade disponível e começa em 1 ao trocar/atualizar o item. Os atalhos de
uma unidade e pilha inteira continuam disponíveis. Pilhas unitárias mantêm a
interface anterior; os itens atuais do templo são unitários.

O mesmo componente confere carga, proximidade e disponibilidade durante a edição
e novamente no clique, após aplicar o texto digitado. A transferência real ainda
revalida o pedido no domínio. Estado vazio desativa e oculta o seletor.

Validação: compilação sem avisos/erros; integração sem janela e gráfica isolada
com saída 0 e `QUANTITY_TRANSFER_OK`. Fixtures de componente verificam edição
digitada, valor exato solicitado na coleta/devolução, limite de carga, mudança
de estado antes do clique, precisão até int32 máximo e limpeza ao esvaziar.
Essas fixtures não transferem itens reais; mapa e bolsa reais ficam intactos.
Os 144 grupos sintéticos existentes cobrem as regras reais de transferência,
quantidades parciais, conservação, carga e checkpoint.

Captura `.local/temple-quantity.quantity.png` revisada em 1440 × 900, identificada
como teste sintético na própria janela. Logs `.local/quantity-build.log`,
`.local/quantity-preview.log`, stdout/stderr e resultado isolado. Nenhuma janela
no desktop ativo.

Próximo: conferir o script do recipiente restante do templo e documentar seus
eventos antes de ampliar o perfil de interação. GPT-6.1 Sol, esforço Alto (High).
