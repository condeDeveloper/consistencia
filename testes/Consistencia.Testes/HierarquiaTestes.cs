using Conde.Consistencia;
using Xunit;

namespace Conde.Consistencia.Testes;

/// <summary>
/// A HIERARQUIA, conferida em milhares de historias de verdade.
///
/// Os livros desenham a hierarquia como uma pilha de caixas. Aqui ela e um
/// enunciado testavel sobre cada historia, e os testes sao as implicacoes:
///
///   linearizavel  implica  sequencialmente consistente
///   linearizavel  implica  todas as leituras regulares
///   linearizavel  implica  nenhuma leitura andou para tras
///
/// E nenhuma das voltas vale, o que tambem e testado: para cada implicacao, uma
/// historia que cumpre o lado fraco e nao o forte.
/// </summary>
public class HierarquiaTestes
{
    private static readonly Especificacao Registrador = Especificacao.Registrador();

    private static IEnumerable<Historia> Historias(int quantas = 300, int n = 3, int w = 1, int r = 1)
    {
        for (ulong semente = 1; semente <= (ulong)quantas; semente++)
        {
            var pedidos = Execucao.Sortear(3, 4, 0.4, semente);
            yield return Execucao.Rodar(pedidos, n, w, r, semente * 7 + 1);
        }
    }

    /// <summary>
    /// TODA historia linearizavel e sequencialmente consistente. Esta e a
    /// implicacao que, se falhasse, denunciaria um erro em um dos dois juizes.
    /// </summary>
    [Fact]
    public void TodaLinearizavelEhSequencialmenteConsistente()
    {
        var linearizaveis = 0;

        foreach (var (n, w, r) in new[] { (3, 1, 1), (3, 2, 1), (3, 2, 2), (5, 3, 3) })
            foreach (var historia in Historias(200, n, w, r))
            {
                if (!Linearizavel.Eh(historia, Registrador)) continue;

                linearizaveis++;
                Assert.True(Sequencial.Eh(historia, Registrador));
            }

        Assert.True(linearizaveis > 200, $"so {linearizaveis} historias linearizaveis no total");
    }

    /// <summary>
    /// E a VOLTA e falsa em centenas de historias: sequencialmente consistentes e
    /// nao linearizaveis.
    /// </summary>
    [Fact]
    public void AVoltaEhFalsaEmMuitasHistorias()
    {
        var soFracas = Historias(300)
            .Count(h => Sequencial.Eh(h, Registrador) && !Linearizavel.Eh(h, Registrador));

        Assert.True(soFracas > 20, $"so {soFracas} historias separaram as duas definicoes");
    }

    /// <summary>
    /// TODA historia linearizavel tem todas as leituras regulares.
    /// </summary>
    [Fact]
    public void TodaLinearizavelTemLeiturasRegulares()
    {
        foreach (var (n, w, r) in new[] { (3, 1, 1), (3, 2, 2) })
            foreach (var historia in Historias(200, n, w, r))
            {
                if (!Linearizavel.Eh(historia, Registrador)) continue;

                Assert.Empty(Garantias.LeiturasNaoRegulares(historia));
            }
    }

    /// <summary>
    /// E a VOLTA e falsa: historias com todas as leituras regulares e que nao sao
    /// linearizaveis.
    ///
    /// Esta e a diferenca que importa na pratica, porque e exatamente a que o
    /// quorum com R + W maior que N entrega: cada leitura, sozinha, e defensavel;
    /// duas leituras seguidas podem andar para tras.
    /// </summary>
    [Fact]
    public void RegularNaoImplicaLinearizavel()
    {
        var regularesENaoLineares = Historias(200, 3, 2, 2)
            .Count(h => Garantias.LeiturasNaoRegulares(h).Count == 0 && !Linearizavel.Eh(h, Registrador));

        Assert.True(regularesENaoLineares > 0, "nenhuma historia separou regular de linearizavel");
    }

    /// <summary>
    /// O ACHADO QUE ME CORRIGIU: "leitura monotonica" medida pela ordem das
    /// VERSOES nao e consequencia da linearizabilidade.
    ///
    /// Eu tinha escrito este teste exigindo zero, e ele falhou. O motivo e
    /// especifico e correto: a versao e atribuida quando a escrita COMECA, e a
    /// linearizacao e livre para ordenar duas escritas CONCORRENTES ao contrario
    /// disso. Um processo pode entao ler a versao 7 e depois a 6, com a
    /// linearizacao pondo a 7 antes da 6, e a historia continua linearizavel.
    ///
    /// A garantia de leitura monotonica do livro fala da ordem da LINEARIZACAO, e
    /// nao de nenhuma numeracao de fora. Medir com a numeracao de fora e util (e o
    /// que um cliente consegue observar) e nao e a mesma coisa.
    ///
    /// Na pratica e raro: em oitocentas historias linearizaveis, tres.
    /// </summary>
    [Fact]
    public void ALeituraMonotonicaNaoEhConsequenciaDaLinearizabilidade()
    {
        var linearizaveis = 0;
        var comRetrocesso = 0;

        foreach (var (n, w, r) in new[] { (3, 1, 1), (3, 2, 2), (3, 3, 1), (5, 3, 3) })
            foreach (var historia in Historias(300, n, w, r))
            {
                if (!Linearizavel.Eh(historia, Registrador)) continue;

                linearizaveis++;
                if (Garantias.LeiturasQueAndaramParaTras(historia) > 0) comRetrocesso++;

                // esta, sim, nunca aparece: a propria escrita do processo precede
                // a leitura dele no relogio, entao toda linearizacao a poe antes
                Assert.Equal(0, Garantias.LeiturasQueNaoViramAPropriaEscrita(historia));
            }

        Assert.True(linearizaveis > 500, $"so {linearizaveis} historias linearizaveis");
        Assert.True(comRetrocesso > 0, "nenhuma historia linearizavel andou para tras: o achado sumiu");
        Assert.True((double)comRetrocesso / linearizaveis < 0.02, $"{comRetrocesso} de {linearizaveis} e demais");
    }

    /// <summary>
    /// E nenhuma historia, de nenhuma configuracao, devolve valor INVENTADO.
    ///
    /// Esta e a unica propriedade que o armazem replicado cumpre sempre, em toda
    /// configuracao, ate na mais fraca possivel. Consistencia fraca permite valor
    /// velho, nao permite valor do nada.
    /// </summary>
    [Fact]
    public void NenhumaConfiguracaoInventaValor()
    {
        foreach (var (n, w, r) in new[] { (3, 1, 1), (3, 2, 1), (3, 1, 2), (3, 2, 2), (5, 1, 1), (5, 3, 3) })
            foreach (var historia in Historias(100, n, w, r))
                Assert.Equal(0, Garantias.LeiturasDeValorInventado(historia));
    }

    /// <summary>
    /// O retrato junta tudo e tem que concordar com cada checagem feita em
    /// separado.
    /// </summary>
    [Fact]
    public void ORetratoConcordaComAsChecagensSeparadas()
    {
        foreach (var historia in Historias(100))
        {
            var retrato = Garantias.Retratar(historia, Registrador);

            Assert.Equal(Linearizavel.Eh(historia, Registrador), retrato.Linearizavel);
            Assert.Equal(Sequencial.Eh(historia, Registrador), retrato.SequencialmenteConsistente);
            Assert.Equal(Garantias.LeiturasNaoRegulares(historia).Count, retrato.LeiturasNaoRegulares);
            Assert.Equal(Garantias.LeiturasQueAndaramParaTras(historia), retrato.AndaramParaTras);
        }
    }
}
