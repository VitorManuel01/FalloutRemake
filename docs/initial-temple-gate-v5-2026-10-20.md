# Porta final no estado inicial e checkpoint v5

O perfil verificado de `aitemdor` permite abrir/fechar a porta ID 104, offset
462744, hex 19928, elevação 2, antes do desafio. A entrada inicial define locais
0/1/3 como 1: destrancada e armadilha inativa. A exceção do hex 13528 pertence
a outra instância. A variável fonte do mapa 20 deve continuar zero neste perfil.

A correspondência exige MAP, instância, INT, PRO, vínculo, registro ativo e
hash/tamanho da lista de scripts conhecidos. Não libera portas desconhecidas,
arrombamento, chave, armadilha, XP ou mudanças de fase do desafio. Nenhum NPC
é executado; não há chamada implícita à VM ou ao banco de estado destacado.

O checkpoint passa a **v5 / neutral-v5** para autorizar esse estado novo de porta.
As versões 1–4 continuam carregando com o estado original fechado; não podem
injetar a abertura que seus schemas não suportavam. No v5, flags de tranca e
emperramento continuam recusadas para essa porta. Salvar sobre o hex aberto e
retomar nessa posição é validado. Não foram adicionados bytes de assets.

Cinco grupos adicionais cobrem perfil, opt-in, movimento/proximidade/ocupação,
recusa de arrombamento e corrupção, checkpoint aberto e migração 1–4.
**262 grupos sintéticos passaram**. A auditoria real, destacada da sessão
chamadora, comprovou:

- Aproximação à porta pelo hex 20127.
- Área alcançável da elevação 2: **974 → 1321 hexes**.
- Aproximação ao NPC pelo hex **13928**, com percurso validado.
- Checkpoints sobre a porta aberta e ao lado do NPC; XP/itens/fonte preservados.

Evidência: `.local/initial-gate-real-audit.json`. O relatório de continuidade
executa a auditoria em uma sessão restaurada separada e confirma que o chamador
permanece byte a byte igual. O checkpoint do usuário não é tocado.

Próximo: validar a interação U e os mesmos checkpoints na cena e deixar o limite
do desafio claro no inspetor. GPT-6.1 Sol, esforço Alto.
