namespace Conde.Consistencia;

/// <summary>
/// O ARMAZEM REPLICADO, com quorum de leitura e de escrita.
///
/// Tres parametros: N replicas, W replicas tocadas por uma escrita, R replicas
/// lidas por uma leitura. A regra famosa e R + W maior que N, e a ideia dela e
/// uma so: dois conjuntos de tamanhos R e W dentro de N precisam ter pelo menos
/// um elemento em comum, entao toda leitura encontra pelo menos uma replica que
/// viu a ultima escrita.
///
/// O que a regra promete e MENOS do que quase todo mundo le nela, e o modelo
/// daqui mostra por que: ela fala de escritas CONCLUIDAS. Enquanto uma escrita
/// esta em andamento, parte das replicas ja mudou e parte nao, e duas leituras
/// consecutivas podem cair em lados diferentes da escrita e ANDAR PARA TRAS.
///
/// Por isso a escrita aqui nao e atomica: ela e uma sequencia de passos, um por
/// replica, que se intercalam com os passos das outras operacoes. E assim que a
/// rede funciona, e e a diferenca entre a regra e a garantia.
/// </summary>
public sealed class Replicado
{
    private readonly int[] valor;
    private readonly long[] versao;
    private long relogio;

    public Replicado(int replicas, int inicial = 0)
    {
        if (replicas <= 0) throw new ArgumentOutOfRangeException(nameof(replicas));

        Replicas = replicas;
        valor = new int[replicas];
        versao = new long[replicas];
        Array.Fill(valor, inicial);
    }

    public int Replicas { get; }

    /// <summary>A versao que a proxima escrita vai carregar.</summary>
    public long ProximaVersao() => ++relogio;

    public void Gravar(int replica, int novo, long comQueVersao)
    {
        if (comQueVersao <= versao[replica]) return;

        valor[replica] = novo;
        versao[replica] = comQueVersao;
    }

    public (int Valor, long Versao) Ler(int replica) => (valor[replica], versao[replica]);

    /// <summary>
    /// O REPARO DE LEITURA: depois de ler, escrever de volta o vencedor nas
    /// replicas atrasadas.
    ///
    /// E a peca que quase nunca aparece no desenho do quorum e que muda o
    /// resultado da medida. Sem ela, uma leitura que viu o valor novo nao deixa
    /// rastro, e a proxima leitura pode ver o velho de novo.
    /// </summary>
    public void Reparar(int[] replicas, int vencedor, long versaoVencedora)
    {
        ArgumentNullException.ThrowIfNull(replicas);
        foreach (var r in replicas) Gravar(r, vencedor, versaoVencedora);
    }
}

/// <summary>Um passo de uma operacao: um acesso a uma replica.</summary>
public sealed record Passo(int Operacao, int Replica);

/// <summary>
/// A EXECUCAO: monta uma historia de verdade rodando operacoes contra o armazem
/// replicado, com os passos de cada uma intercalados pelo sorteio.
/// </summary>
public static class Execucao
{
    public sealed record Pedido(int Processo, Acao Acao, int Argumento);

    /// <summary>
    /// Roda os pedidos e devolve a historia que os clientes observaram.
    ///
    /// Os passos de todas as operacoes sao embaralhados respeitando a ordem de
    /// cada processo: um processo so comeca a proxima operacao depois de a
    /// anterior terminar, que e o que um cliente sincrono faz. Entre processos
    /// diferentes, tudo se mistura.
    /// </summary>
    public static Historia Rodar(List<Pedido> pedidos, int replicas, int escrita, int leitura, ulong semente, bool comReparo = false)
    {
        ArgumentNullException.ThrowIfNull(pedidos);

        var armazem = new Replicado(replicas);
        var sorteio = new Sorteio(semente);

        // as replicas que cada operacao vai tocar, e em que ordem
        var alvos = new List<int[]>();
        foreach (var pedido in pedidos)
            alvos.Add(sorteio.Escolher(pedido.Acao == Acao.Escrever ? escrita : leitura, replicas));

        // a fila de cada processo
        var processos = pedidos.Count == 0 ? 0 : pedidos.Max(p => p.Processo) + 1;
        var daFila = new List<int>[processos];
        for (var p = 0; p < processos; p++) daFila[p] = [];
        for (var i = 0; i < pedidos.Count; i++) daFila[pedidos[i].Processo].Add(i);

        var proximaDoProcesso = new int[processos];
        var passoDaOperacao = new int[pedidos.Count];
        var versaoDaEscrita = new long[pedidos.Count];
        var melhorValor = new int[pedidos.Count];
        var melhorVersao = new long[pedidos.Count];
        var comeco = new int[pedidos.Count];
        var fim = new int[pedidos.Count];

        Array.Fill(comeco, -1);
        Array.Fill(melhorVersao, -1);

        var tempo = 0;
        var faltam = pedidos.Count;

        while (faltam > 0)
        {
            // os processos que tem uma operacao em andamento ou pronta para
            // comecar
            var podem = new List<int>();
            for (var p = 0; p < processos; p++)
                if (proximaDoProcesso[p] < daFila[p].Count) podem.Add(p);

            if (podem.Count == 0) break;

            var processo = podem[sorteio.Ate(podem.Count)];
            var operacao = daFila[processo][proximaDoProcesso[processo]];
            var pedido = pedidos[operacao];

            if (comeco[operacao] < 0) comeco[operacao] = tempo;

            var replica = alvos[operacao][passoDaOperacao[operacao]];

            if (pedido.Acao == Acao.Escrever)
            {
                // O VALOR ESCRITO E A PROPRIA VERSAO.
                //
                // Assim a historia carrega a ordem real em que as escritas
                // comecaram, sem precisar de um mapa a parte, e os valores sao
                // distintos de graca. Com o valor vindo do pedido, "a leitura
                // devolveu 5 e depois 3" nao diria nada: 5 e 3 eram so dois
                // rotulos sorteados, sem ordem entre eles.
                if (versaoDaEscrita[operacao] == 0) versaoDaEscrita[operacao] = armazem.ProximaVersao();
                armazem.Gravar(replica, (int)versaoDaEscrita[operacao], versaoDaEscrita[operacao]);
            }
            else
            {
                var (v, ver) = armazem.Ler(replica);
                if (ver > melhorVersao[operacao]) (melhorValor[operacao], melhorVersao[operacao]) = (v, ver);
            }

            passoDaOperacao[operacao]++;
            tempo++;

            if (passoDaOperacao[operacao] == alvos[operacao].Length)
            {
                fim[operacao] = tempo;
                proximaDoProcesso[processo]++;
                faltam--;

                if (comReparo && pedido.Acao == Acao.Ler && melhorVersao[operacao] > 0)
                    armazem.Reparar(alvos[operacao], melhorValor[operacao], melhorVersao[operacao]);
            }
        }

        var operacoes = new List<Operacao>();

        for (var i = 0; i < pedidos.Count; i++)
        {
            var pedido = pedidos[i];
            var resposta = pedido.Acao == Acao.Ler ? melhorValor[i] : 0;
            var argumento = pedido.Acao == Acao.Escrever ? (int)versaoDaEscrita[i] : 0;

            operacoes.Add(new Operacao(pedido.Processo, pedido.Acao, argumento, resposta, comeco[i], fim[i]));
        }

        return new Historia(operacoes);
    }

    /// <summary>
    /// Pedidos sorteados: alguns processos escrevendo valores distintos e outros
    /// lendo.
    ///
    /// Os valores escritos sao todos diferentes de proposito. Com valores
    /// repetidos, uma violacao de consistencia pode ficar invisivel: a leitura
    /// devolve o valor certo pelo motivo errado.
    /// </summary>
    public static List<Pedido> Sortear(int processos, int porProcesso, double fracaoDeEscrita, ulong semente)
    {
        var sorteio = new Sorteio(semente);
        var pedidos = new List<Pedido>();
        var proximoValor = 1;

        for (var i = 0; i < porProcesso; i++)
            for (var p = 0; p < processos; p++)
                pedidos.Add(sorteio.Fracao() < fracaoDeEscrita
                    ? new Pedido(p, Acao.Escrever, proximoValor++)
                    : new Pedido(p, Acao.Ler, 0));

        return pedidos;
    }
}
