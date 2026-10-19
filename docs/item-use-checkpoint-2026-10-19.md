# Marco 51 — checkpoint de itens posicionados e consumidos

Novos checkpoints usam versão 3 / `neutral-v3`, com `Taken` e `Uses` explícitos
em cada área. A lista de usos preserva GUID, chaves fonte, quantidade um,
local, fase, prazo e ticks restantes. A bolsa é derivada da parte ainda carregada;
posicionados/consumidos não reaparecem nela ou no recipiente ao carregar.

V1 continua com bolsa/usos neutros; v2 mantém suas retiradas e usa lista neutra
de usos. Carregar e salvar qualquer versão aceita produz v3. Versões antigas
não aceitam alocações injetadas. Campos obrigatórios ausentes no v3 são recusados.

O mundo candidato valida GUIDs globalmente únicos, balanços, perfil/hash do
explosivo e área/elevação de posicionamento presente no checkpoint. Saídas e
bloqueios permanentes são recusados sem carregar áreas desconhecidas. Um item
pode continuar sob uma porta suportada fechada depois de seu posicionamento;
a restauração não exige que esse hex continue livre para o personagem.
Essa exceção não libera portas de scripts desconhecidos. Navegação é reutilizada
por área/elevação durante a conferência de múltiplas alocações.

Fase, ticks restantes, prazo e coordenadas exigem campos JSON explícitos: omiti-los
não pode transformar uma unidade consumida em evento pendente ou antecipar seu prazo.
Os arquivos v1/v2 válidos já possuem as coordenadas completas e continuam aceitos.

Validação: 181 grupos sintéticos aprovados, incluindo estado misto, origem
diferente da área de posicionamento, v1/v2 sem campos novos, duplicação entre
áreas, fontes modificadas, geometria e conservação. O relatório real reproduz
salvar/carregar uma unidade original e sua fase consumida numa sessão semeada
para teste, sem realizar uma coleta ou explosão jogável:

```powershell
dotnet run --project tools/TempleInspector -- --data-dir 'D:\Games\fallout2\Fallout 2 English' --item-use-checkpoint-report
```

A suíte Godot também carrega v1/v2 com o pó original em área inativa e volta ao
v3, usando somente o slot de teste. A bateria unificada passa a ter onze etapas.
Nenhuma ação de ativação, relógio, dano ou modificação da barreira foi ligada.

Próximo: commit de uma proposta com token/revisão e revalidação de posição e
quantidade; manter a ação fora da interface até concluir seus demais requisitos.
GPT-6.1 Sol, esforço Alto.
