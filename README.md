# consistencia

Modelos de consistência do zero em C# e .NET 8: linearizabilidade, consistência
sequencial, registrador regular, garantias de sessão e quóruns de leitura e
escrita. O juiz é a **busca exaustiva por uma linearização**, que é a definição de
Herlihy e Wing escrita como algoritmo.

```
$ dotnet medidor.dll quorum

  N   W   R   R+W>N   linearizaveis   so sequenciais   leituras nao regulares
  3   1   1     nao               2               34                      773
  3   2   1     nao              31               57                      225
  3   1   2     nao              33               59                      332
  3   2   2     sim             188                5                        0
  3   3   1     sim             146               30                        0
  5   3   3     sim             193                1                        0
```

**A regra R + W maior que N entrega leituras regulares, e não entrega
linearizabilidade.** Com a regra valendo, nenhuma leitura sozinha é
indefensável: a coluna da direita é zero. E ainda assim 12 de 200 histórias não
são linearizáveis, porque a regra não diz nada sobre **duas** leituras seguidas.

## As duas definições, na história que as separa

```
$ dotnet medidor.dll juiz

    p0 escreveu 1 [0,1]
    p1 leu 0 [2,3]

    linearizavel:                 nao
    sequencialmente consistente:  sim
```

As duas definições são a mesma busca com **uma condição a menos**.
Linearizabilidade exige uma ordem total que respeite o **relógio de parede**;
consistência sequencial exige só que ela respeite a **ordem de cada processo**.

Essa linha a menos é a diferença entre "o sistema parece uma máquina só" e "o
sistema parece uma máquina só, e ela é pontual". A história acima é o sistema que
todo mundo já usou: você salva, avisa o colega, ele recarrega e vê o valor velho.
Nenhuma regra foi quebrada.

E consistência fraca permite valor **velho**, não permite valor **inventado**:

```
    p0 escreveu 1 [0,1]
    p1 leu 7 [2,3]

    linearizavel:                 nao
    sequencialmente consistente:  nao
```

## A hierarquia em histórias de verdade

```
    historias:                                      600
    linearizaveis:                                    8
    so sequencialmente consistentes:                 81
    nenhuma das duas:                               511
    linearizaveis e nao sequenciais (impossivel):      0
```

A última linha precisa ser zero: história linearizável e não sequencialmente
consistente não existe, e se aparecesse seria erro do juiz, não do sistema. Ela
apareceu, e está na lista do que as medidas me corrigiram.

## O que o usuário sente

```
$ dotnet medidor.dll sessao

  N   W   R   leitura andou para tras   nao viu a propria escrita   valor inventado
  3   1   1                      138                         366                 0
  3   2   1                       87                         135                 0
  3   2   2                        8                           0                 0
  3   3   1                       31                           0                 0
  5   3   3                        9                           0                 0
```

"Não é linearizável" não quer dizer nada para quem usa o sistema. Estas três
colunas querem:

**Leitura andou para trás** é a reclamação mais comum de todas: a página mostra o
dado novo, você aperta atualizar e ela mostra o velho. Cada leitura, sozinha,
estava certa.

**Não viu a própria escrita** é a que parece perda de dado: o usuário salvou,
recarregou, e o que ele escreveu não está lá. Para os outros usuários, atraso é
tolerado; para quem escreveu, não.

**Valor inventado** é zero em toda configuração, inclusive na mais fraca de
todas. É a linha entre "mais fraco" e "errado".

## O reparo de leitura não é enfeite

```
$ dotnet medidor.dll reparo

  jeito          linearizaveis   leitura andou para tras   leituras nao regulares
  sem reparo               188                         8                        0
  com reparo               200                         0                        0
```

Escrever de volta o vencedor nas réplicas atrasadas, depois de ler, é a peça que
quase nunca aparece no desenho do quórum. Ela leva 188 histórias linearizáveis a
200 e zera os retrocessos, sem mudar N, W nem R.

A razão é direta: sem ela, uma leitura que viu o valor novo não deixa rastro, e a
leitura seguinte pode cair nas réplicas que ainda não receberam a escrita.

## Concorrência é o que cobra o preço

```
  processos   pares concorrentes   linearizaveis
          1               0.0 %         100.0 %
          2              11.1 %          99.5 %
          3              14.0 %          94.0 %
          4              15.7 %          89.5 %
```

Com um processo só, tudo é linearizável, e isso é o controle do experimento: o
problema não está no modelo, está na concorrência. Com quatro, uma em cada dez
histórias não é.

## O que as medidas me corrigiram

**O intervalo de uma operação é fechado no começo e aberto no fim, e eu tinha
usado o "menor" estrito nos dois lados.** O resultado foi uma coisa que não pode
existir: 12 de 600 histórias saíram linearizáveis **e não** sequencialmente
consistentes. Linearizabilidade é estritamente mais forte, então isso só podia ser
erro meu.

Era: com o "menor" estrito, duas operações seguidas do mesmo processo, uma
terminando no instante 5 e a outra começando no 5, apareciam como **concorrentes**.
A busca passava a aceitar ordens que invertem o programa de um cliente, que é
exatamente o que a definição fraca proíbe e a forte também. A linha impossível foi
o que denunciou.

**"Leitura monotônica" medida pela ordem das versões não é consequência da
linearizabilidade.** Eu tinha escrito o teste exigindo zero e ele falhou. A versão
é atribuída quando a escrita **começa**, e a linearização é livre para ordenar duas
escritas **concorrentes** ao contrário disso: um processo pode ler a versão 7 e
depois a 6, com a linearização pondo a 7 antes da 6, e a história continua
linearizável. A garantia do livro fala da ordem da **linearização**, não de uma
numeração de fora. Em oitocentas histórias linearizáveis, três.

**O valor escrito precisa carregar a ordem das escritas, senão a medida não mede
nada.** Na primeira versão, o valor vinha do pedido do cliente e "a leitura
devolveu 5 e depois 3" não dizia coisa alguma: 5 e 3 eram dois rótulos sorteados,
sem ordem entre eles. O conserto é o valor **ser** a versão.

**A escrita não pode ser atômica no modelo, senão não há o que medir.** Com a
escrita acontecendo de uma vez, o quórum com R + W maior que N dá linearizabilidade
e a tabela inteira fica trivial. A escrita de verdade toca uma réplica de cada vez,
e é no meio dela que as duas leituras caem em lados diferentes.

## Por que o juiz é a busca exaustiva

A definição de linearizabilidade é um **existe**: existe uma ordem total que
respeita o relógio e que a especificação aceita. Não dá para verificar olhando a
história, tem que procurar a ordem. A busca de Wing e Gong procura todas, sem
heurística e sem poda esperta: a única poda é a própria especificação recusar.

E o juiz tem juiz. A ordem que ele devolve é **refeita sem a busca**: um teste
confere que ela respeita o relógio e que a especificação aceita cada passo. As
histórias sequenciais, cujo veredicto não tem ambiguidade, conferem os dois lados.
E a hierarquia inteira é testada como implicação, em milhares de histórias.

A especificação sequencial é parte da definição e quase sempre fica de fora da
conversa: dizer "o sistema é linearizável" sem dizer linearizável em relação a
**quê** não significa nada. Aqui a mesma história é válida para um contador e
inválida para um registrador, e o teste mostra isso.

O sorteio é de um gerador congruente escrito aqui, e não do sorteio da
biblioteca, cuja documentação não promete a mesma sequência entre versões nem
entre sistemas. A integração contínua roda em três: com o gerador próprio, a
semente 7 produz exatamente o mesmo intercalamento nos três.

Nenhuma medida usa relógio de parede para medir custo. O relógio aparece **dentro**
do modelo, como a ordem dos eventos, que é o assunto.

## As peças

| arquivo | o que faz |
| --- | --- |
| `Historia.cs` | operações com intervalo, o sorteio e a relação de tempo real |
| `Especificacao.cs` | o registrador e o contador: o que o objeto faz sozinho |
| `Linearizavel.cs` | a busca exaustiva por uma linearização: o juiz |
| `Sequencial.cs` | a mesma busca com uma condição a menos |
| `Replicado.cs` | as réplicas, os quóruns, o reparo de leitura e a execução |
| `Garantias.cs` | regular, leitura monotônica, leia o que você escreveu |
| `Medidas.cs` | as tabelas que o medidor imprime e que os testes conferem |

## Como rodar

```
dotnet test testes/Consistencia.Testes/Consistencia.Testes.csproj -c Release
dotnet run --project ferramentas/Medidor/Medidor.csproj -c Release -- tudo
```

As medidas aceitam `juiz`, `quorum`, `sessao`, `reparo`, `concorrencia` e `tudo`.

## Licença

MIT.
