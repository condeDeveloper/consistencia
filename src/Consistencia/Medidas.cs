namespace Conde.Consistencia;

/// <summary>
/// As MEDIDAS: as tabelas que o medidor imprime e que os testes conferem.
///
/// Elas ficam aqui, e nao no medidor, para o teste poder afirmar sobre o MESMO
/// numero que aparece no texto. Tabela de README que ninguem testa envelhece
/// errado em silencio.
///
/// Nenhuma delas usa relogio de parede para medir nada. O relogio aparece DENTRO
/// do modelo, como a ordem dos eventos, que e o assunto; o custo e sempre contado
/// em historias e em violacoes.
/// </summary>
public static class Medidas
{
    public const int Processos = 3;
    public const int PorProcesso = 4;

    private static Historia Uma(int replicas, int escrita, int leitura, ulong semente, bool comReparo = false)
    {
        var pedidos = Execucao.Sortear(Processos, PorProcesso, 0.4, semente);
        return Execucao.Rodar(pedidos, replicas, escrita, leitura, semente * 7 + 1, comReparo);
    }

    public sealed record LinhaQuorum(int Replicas, int Escrita, int Leitura, bool RegraVale, int Historias, int Linearizaveis, int Sequenciais, int NaoRegulares);

    /// <summary>
    /// O QUORUM contra o juiz: quantas historias sao linearizaveis, com e sem a
    /// regra R + W maior que N.
    /// </summary>
    public static List<LinhaQuorum> Quoruns(int historias = 200, bool comReparo = false)
    {
        var linhas = new List<LinhaQuorum>();
        var especificacao = Especificacao.Registrador();

        foreach (var (n, w, r) in new[] { (3, 1, 1), (3, 2, 1), (3, 1, 2), (3, 2, 2), (3, 3, 1), (5, 3, 3) })
        {
            var linearizaveis = 0;
            var sequenciais = 0;
            var naoRegulares = 0;

            for (var i = 0; i < historias; i++)
            {
                var historia = Uma(n, w, r, (ulong)(i + 1), comReparo);

                if (Linearizavel.Eh(historia, especificacao)) linearizaveis++;
                if (Sequencial.Eh(historia, especificacao)) sequenciais++;
                naoRegulares += Garantias.LeiturasNaoRegulares(historia).Count;
            }

            linhas.Add(new LinhaQuorum(n, w, r, w + r > n, historias, linearizaveis, sequenciais, naoRegulares));
        }

        return linhas;
    }

    public sealed record LinhaSessao(int Replicas, int Escrita, int Leitura, int AndaramParaTras, int NaoViramAPropriaEscrita, int Inventados);

    /// <summary>
    /// As GARANTIAS DE SESSAO: o que o usuario sente quando a historia nao e
    /// linearizavel.
    /// </summary>
    public static List<LinhaSessao> Sessoes(int historias = 200, bool comReparo = false)
    {
        var linhas = new List<LinhaSessao>();

        foreach (var (n, w, r) in new[] { (3, 1, 1), (3, 2, 1), (3, 2, 2), (3, 3, 1), (5, 3, 3) })
        {
            var tras = 0;
            var propria = 0;
            var inventados = 0;

            for (var i = 0; i < historias; i++)
            {
                var historia = Uma(n, w, r, (ulong)(i + 1), comReparo);

                tras += Garantias.LeiturasQueAndaramParaTras(historia);
                propria += Garantias.LeiturasQueNaoViramAPropriaEscrita(historia);
                inventados += Garantias.LeiturasDeValorInventado(historia);
            }

            linhas.Add(new LinhaSessao(n, w, r, tras, propria, inventados));
        }

        return linhas;
    }

    public sealed record LinhaReparo(string Jeito, int Linearizaveis, int AndaramParaTras, int NaoRegulares);

    /// <summary>
    /// O REPARO DE LEITURA: a peca que quase nunca aparece no desenho do quorum.
    /// </summary>
    public static List<LinhaReparo> Reparos(int historias = 200, int n = 3, int w = 2, int r = 2)
    {
        var linhas = new List<LinhaReparo>();
        var especificacao = Especificacao.Registrador();

        foreach (var (nome, comReparo) in new[] { ("sem reparo", false), ("com reparo", true) })
        {
            var linearizaveis = 0;
            var tras = 0;
            var naoRegulares = 0;

            for (var i = 0; i < historias; i++)
            {
                var historia = Uma(n, w, r, (ulong)(i + 1), comReparo);

                if (Linearizavel.Eh(historia, especificacao)) linearizaveis++;
                tras += Garantias.LeiturasQueAndaramParaTras(historia);
                naoRegulares += Garantias.LeiturasNaoRegulares(historia).Count;
            }

            linhas.Add(new LinhaReparo(nome, linearizaveis, tras, naoRegulares));
        }

        return linhas;
    }

    public sealed record LinhaHierarquia(int Historias, int Linearizaveis, int SequenciaisSoMenteS, int Nenhuma, int LinearizavelENaoSequencial);

    /// <summary>
    /// A HIERARQUIA conferida em historias de verdade: toda linearizavel e
    /// sequencialmente consistente, e a volta e falsa em muitas.
    ///
    /// A ultima coluna e a que precisa ser ZERO sempre: uma historia linearizavel
    /// e nao sequencialmente consistente seria um erro do juiz, nao do sistema.
    /// </summary>
    public static LinhaHierarquia Hierarquia(int historias = 600)
    {
        var especificacao = Especificacao.Registrador();

        var linearizaveis = 0;
        var soSequenciais = 0;
        var nenhuma = 0;
        var impossivel = 0;

        for (var i = 0; i < historias; i++)
        {
            var historia = Uma(3, 1, 1, (ulong)(i + 1));

            var linear = Linearizavel.Eh(historia, especificacao);
            var sequencial = Sequencial.Eh(historia, especificacao);

            if (linear) linearizaveis++;
            if (!linear && sequencial) soSequenciais++;
            if (!linear && !sequencial) nenhuma++;
            if (linear && !sequencial) impossivel++;
        }

        return new LinhaHierarquia(historias, linearizaveis, soSequenciais, nenhuma, impossivel);
    }

    public sealed record LinhaConcorrencia(int Processos, double FracaoConcorrente, int Linearizaveis, int Historias);

    /// <summary>
    /// Quanto mais CONCORRENCIA, menos historias linearizaveis. Nao porque o
    /// sistema piora: porque ha mais pares em que o relogio obriga uma ordem e o
    /// sistema entrega outra.
    /// </summary>
    public static List<LinhaConcorrencia> Concorrencias(int historias = 200)
    {
        var linhas = new List<LinhaConcorrencia>();
        var especificacao = Especificacao.Registrador();

        foreach (var processos in new[] { 1, 2, 3, 4 })
        {
            var concorrente = 0.0;
            var linearizaveis = 0;

            for (var i = 0; i < historias; i++)
            {
                var pedidos = Execucao.Sortear(processos, 4, 0.4, (ulong)(i + 1));
                var historia = Execucao.Rodar(pedidos, 3, 2, 2, (ulong)(i * 7 + 1));

                var pares = (long)historia.Quantas * (historia.Quantas - 1) / 2;
                concorrente += pares == 0 ? 0 : (double)historia.QuantosParesConcorrentes() / pares;

                if (Linearizavel.Eh(historia, especificacao)) linearizaveis++;
            }

            linhas.Add(new LinhaConcorrencia(processos, concorrente / historias, linearizaveis, historias));
        }

        return linhas;
    }
}
