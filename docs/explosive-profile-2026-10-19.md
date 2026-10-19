# Marco 45 — temporizador e ciclo do explosivo

`ExplosiveTimer.Plan` implementa somente o ramo confirmado do temporizador:
10–180 segundos, passo 10, padrão 60 e dez ticks por segundo. Sucesso e sucesso
crítico preservam o prazo; falha usa metade e evento prematuro; falha crítica
usa prazo zero. Demolition Expert força sucesso. O resultado é imutável e não
ativa, posiciona, consome ou explode nenhum item.

O código recusa cancelamento (-1), valores fora da faixa e resultados de rolagem
desconhecidos. Cancelamento deve ser tratado pelo chamador antes desse cálculo.
Não há relógio, rolagem de Traps ou ação de uso ligada à prévia.

Na instalação Epic, o explosivo do pote ID 358 é a unidade ID 364/PID 85;
o PRO ativo é PID 209. Ambos têm peso base 4 lb. O relatório confere os dois
PROs sem alterar o MAP, guarda suas identidades e mostra os quatro resultados
de prazo para uma escolha de 60 segundos:

```powershell
dotnet run --project tools/TempleInspector -- --data-dir 'D:\Games\fallout2\Fallout 2 English' --explosive-profile-report
```

CE fixado, `inventory.cc:5582/5971`, `proto_instance.cc:869/1115`,
`item.cc:3459`, `queue.cc:457`: a ativação troca PID 85 por 209; o wrapper
retira uma unidade e a coloca no tile/elevação do proprietário. A expiração
usa o local atual do proprietário se ainda carregada, senão o tile de chão.
Só destrói o objeto após a explosão aceitar; resultado busy (-2) reagenda 50 ticks.

Há duas questões explícitas antes de um adaptador jogável:

- O helper `explosiveGetDamage` fixado reconhece o PID inativo 85, enquanto a
  fila o chama com o PID ativo 209 e ignora o retorno falso. Não inferimos dano
  válido desse caminho; o futuro perfil deve resolver o PID ativo explicitamente.
- O handler retorna 2 mesmo após cancelar o diálogo; o wrapper pode retirar e
  largar a unidade ainda inativa. Essa observação estática exige uma decisão
  de compatibilidade, sem habilitar o comportamento por acidente.

Validação: build sem erros/avisos, 163 grupos sintéticos aprovados e perfil real
de dados. Não foi executado o motor CE para observar essas duas questões.
Os valores CE padrão 40–80 de dano não estão ligados a dano em criaturas.

Próximo independente: levar os ícones à consulta I dos inventários originais.
Para o uso jogável ainda faltam propriedade/consumo, relógio e checkpoint da
barreira. GPT-6.1 Sol, esforço Alto.
