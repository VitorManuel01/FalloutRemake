# Aproximação e alcance para posicionar — 20/10/2026

Na bolsa, o explosivo original oferece **Ir à barreira** quando o personagem
está na elevação 1 e existe caminho para um ponto dentro do alcance conhecido.
O destino é o ponto alcançável mais próximo do personagem dentro de três hexes
da barreira original, excluindo saídas. O clique fecha a bolsa, seleciona a
barreira e solicita/enquadra a caminhada normal; não teleporta nem usa o item.

Ao chegar, abra B e use **Posicionar · ensaio**. A preparação da interface agora
exige a barreira ainda fechada e o local dentro do alcance do perfil validado.
Fora desse perfil, a unidade permanece na bolsa e a dica explica a aproximação.
Isso evita criar eventos sem resolução disponível. A regra é do ensaio da
interface; as propostas gerais de domínio e os checkpoints existentes não mudam.

O perfil da barreira/protótipo é conferido contra os hashes já usados pelo efeito;
o snapshot é invalidado ao trocar de sessão. Movimento, item indisponível e
inspeção de outra elevação continuam recusados antes do posicionamento.

`BARRIER_PLACEMENT_GUIDANCE_OK` usa a coleta real na elevação 1. Confere a recusa
fora do alcance, disponibilidade e dica na bolsa, rota exata, ator ainda no ponto
inicial após o clique, chegada por caminhada dentro do raio e habilitação da
preparação. O checkpoint completo difere somente pela posição legítima do ator.
O ciclo de relógio/efeito/checkpoint segue sendo testado depois dessa chegada.

Resultado: build sem avisos/erros, 227 grupos sintéticos e 13/13 etapas completos;
captura isolada 1024×768 sem erros e desktop preservado. Manifesto:
`.local/temple-validation-20261020-105115-5266b105/manifest.json`.
