# Marco 56 — proposta de substituição da barreira

`TempleBarrierEffectProposal.Create` recebe o GUID de um uso e o protótipo de
substituição carregado pelos dados locais. Aceita somente alocação posicionada
com prazo vencido, no mapa original suportado. Valida mapa, instância 44/offset
303324, script `aibkdor.int`, vínculo ativo e protótipos original/substituto por
campos e identidades SHA-256 do importador. A substituição exige flags exatas
NoBlock/ShootThrough/LightThrough, subtipo 5 e ausência de script.

O alvo precisa estar na mesma elevação e até três hexes da posição registrada.
O mapa ativo e a posição atual do personagem não alteram o centro da proposta.
A consulta usa somente áreas já carregadas. A proposta inclui GUID, token da
sessão, revisão da origem, chave da barreira, PID/FID/hash do substituto e distância.
Não destrói, cria, consome, avança tempo ou concede XP. Não há método de commit
de efeito neste marco; uma proposta antiga ainda não pode ser aplicada.

```powershell
dotnet run --project tools/TempleInspector -- --data-dir 'D:\Games\fallout2\Fallout 2 English' --barrier-effect-report
```

O relatório percorre 182 passos na geometria original: abre a primeira porta,
força sucesso no arrombamento conhecido da segunda, passa à elevação 1, coleta
de fato a unidade 364/PID 85 do pote 358, caminha ao hex 14521 e faz o commit de
posicionamento com sucesso de temporizador controlado. Antes de 600 ticks recusa
o efeito; depois reconhece a barreira a distância 1. Salvar/recarregar preserva a
alocação vencida e gera proposta com o novo token. A região segue com 1245 hexes
alcançáveis e a barreira continua bloqueando. O MAP reconstrói byte a byte.

Os testes sintéticos usam metadados fabricados explicitamente identificados,
sem bytes proprietários. Cobrem prazo, repetição sem mutação, limites 3/4,
elevação, identidades alteradas, substituto inválido, sessão recarregada, área
inativa e origem distinta da área de posicionamento. O relatório real entrou
na bateria, agora com doze etapas e oito relatórios.

Próximo: persistir uma substituição com GUID próprio e aplicá-la à geometria,
revalidando a proposta e consumindo a unidade atomicamente. Nenhum combate geral
é necessário para esse recorte. GPT-6.1 Sol, esforço Alto.
