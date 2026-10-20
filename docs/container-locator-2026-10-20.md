# Localizar recipientes — 20/10/2026

Sob a elevação, **Localizar recipiente…** lista somente os recipientes visíveis
da camada atual. Cada opção inclui o nome original e os itens restantes;
recipientes esgotados mostram “vazio”, os restritos mostram “interação especial”.
O pote do explosivo aparece com **Plastic Explosives**, sem exigir que o usuário
memorize um ID ou hex.

Escolher uma opção seleciona a chave fonte e enquadra a câmera, sem caminhar,
teleportar, abrir ou recolher. Filtros, troca de camada, coleta, devolução e carga
de checkpoint atualizam a lista. Um modal aberto impede a ação.

`CONTAINER_LOCATOR_OK` cobre os sete recipientes nas três elevações, nome do
explosivo, posição da câmera, filtro e modal. A comparação do checkpoint inteiro
confirma que localizar não modifica o mundo; o teste de coleta confirma o rótulo
“vazio”. Captura 1280×720 aprovada pelo validador estrito, com desktop preservado.
Build sem avisos/erros, 227 grupos sintéticos e 14/14 etapas completos.
Manifesto: `.local/temple-validation-20261020-105748-29df3f81/manifest.json`.
