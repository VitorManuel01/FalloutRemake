# Metadados de locais do script e alocação inicial

`ScriptListMetadata` lê uma linha física de `scripts/scripts.lst`, conserva seu
índice, valida o nome e extrai `local_vars=` quando existe `#`, como no CE.
Não compacta linhas vazias/comentários. Declarações ambíguas, negativas,
malformadas ou acima de 4096 são recusadas; não emula as coerções de `atoi`.
O resultado registra identidade/hash da lista e caminho do programa.

Na instalação Epic resolvida, a lista vem de `patch000.dat`, 104.534 bytes,
hash `05AD814B71D80BD3884D80CA4F903EDD1E85CB9B882C89B096B8CF3CADC702F9`.
Índice 748 (`actemvil.int`) declara **16 locais**; índice 749 (`aitemdor.int`)
declara **9**. O MAP grava zero para as contagens dessas duas instâncias.

No CE local, `scriptGetLocalVar`/`scriptSetLocalVar` consultam a lista quando a
contagem da instância é zero. `_map_malloc_local_var` aloca e preenche com zeros.
Isso explica por que a contagem zero no MAP não significa ausência de locais
no jogo. Não foi necessário executar esses programas para confirmar o caminho.

Quatro grupos sintéticos verificam índices, identidade/imutabilidade, linhas
não compactadas, limites, caminho, regra de comentário, zero, números ambíguos
e valores fora do perfil. **246 grupos passaram**. O relatório do templo inclui
as contagens originais e declaradas: `.local/temple-script-local-metadata.json`.

Questão descoberta: Node012a contém leitura literal do local 16, embora a lista
declare 16 entradas (0–15). Não aumentar a contagem nem corrigir a origem por
suposição. Próximo incremento: listar referências fora da contagem declarada,
com offset e procedimento, mantendo o adaptador do NPC desabilitado.
Modelo recomendado: GPT-6.1 Sol, esforço Alto.
