using Conde.Consistencia;
using Xunit;

namespace Conde.Consistencia.Testes;

/// <summary>
/// Os testes das TABELAS que o medidor imprime.
///
/// Eles existem porque numero em README que ninguem confere envelhece errado em
/// silencio: alguem troca uma constante, a tabela muda e o texto continua dizendo
/// a frase antiga. Aqui o teste afirma sobre o mesmo objeto que vira linha
/// impressa.
/// </summary>
public class MedidasTestes
{
    /// <summary>
    /// A linha que precisa ser ZERO: historia linearizavel e nao sequencialmente
    /// consistente nao existe, e se aparecesse seria erro do juiz, nao do
    /// sistema.
    ///
    /// Ela apareceu: doze de seiscentas, por causa do intervalo fechado no fim.
    /// </summary>
    [Fact]
    public void NenhumaHistoriaEhLinearizavelESemSerSequencial()
    {
        var h = Medidas.Hierarquia();

        Assert.Equal(0, h.LinearizavelENaoSequencial);
        Assert.Equal(600, h.Historias);
        Assert.Equal(h.Historias, h.Linearizaveis + h.SequenciaisSoMenteS + h.Nenhuma);
    }

    /// <summary>
    /// E as tres caixas da hierarquia tem gente dentro: se uma estivesse vazia, a
    /// medida nao estaria separando nada.
    /// </summary>
    [Fact]
    public void AsTresCaixasDaHierarquiaTemGenteDentro()
    {
        var h = Medidas.Hierarquia();

        Assert.True(h.Linearizaveis > 0);
        Assert.True(h.SequenciaisSoMenteS > 0);
        Assert.True(h.Nenhuma > 0);
    }

    /// <summary>
    /// O ACHADO PRINCIPAL: a regra R + W maior que N entrega leituras REGULARES e
    /// NAO entrega linearizabilidade.
    ///
    /// Em todas as configuracoes que cumprem a regra, nenhuma leitura sozinha e
    /// indefensavel. E ainda assim nem toda historia e linearizavel, porque a
    /// regra nao diz nada sobre DUAS leituras seguidas.
    /// </summary>
    [Fact]
    public void ARegraEntregaRegularENaoLinearizavel()
    {
        var linhas = Medidas.Quoruns();

        Assert.Equal(6, linhas.Count);

        foreach (var l in linhas.Where(l => l.RegraVale))
        {
            Assert.Equal(0, l.NaoRegulares);
            Assert.True(l.Linearizaveis > l.Historias / 2, $"N{l.Replicas} W{l.Escrita} R{l.Leitura}: so {l.Linearizaveis}");
        }

        Assert.Contains(linhas, l => l.RegraVale && l.Linearizaveis < l.Historias);
    }

    /// <summary>
    /// E sem a regra, as leituras indefensaveis aparecem as centenas.
    /// </summary>
    [Fact]
    public void SemARegraAsLeiturasIndefensaveisAparecem()
    {
        foreach (var l in Medidas.Quoruns().Where(l => !l.RegraVale))
        {
            Assert.True(l.NaoRegulares > 50, $"N{l.Replicas} W{l.Escrita} R{l.Leitura}: so {l.NaoRegulares}");
            Assert.True(l.Linearizaveis < l.Historias / 2);
        }
    }

    /// <summary>
    /// A hierarquia vale linha a linha: as linearizaveis nunca passam das
    /// sequenciais.
    /// </summary>
    [Fact]
    public void AsLinearizaveisNuncaPassamDasSequenciais()
    {
        foreach (var l in Medidas.Quoruns())
            Assert.True(l.Linearizaveis <= l.Sequenciais, $"N{l.Replicas} W{l.Escrita} R{l.Leitura}");
    }

    /// <summary>
    /// A frase do texto: quanto mais fraco o quorum, mais o usuario sente. E
    /// valor inventado nao aparece em configuracao nenhuma.
    /// </summary>
    [Fact]
    public void OQuorumFracoEhOQueOUsuarioSente()
    {
        var linhas = Medidas.Sessoes();

        Assert.Equal(5, linhas.Count);
        Assert.All(linhas, l => Assert.Equal(0, l.Inventados));

        var maisFraco = linhas.Single(l => l.Replicas == 3 && l.Escrita == 1 && l.Leitura == 1);
        var maisForte = linhas.Single(l => l.Replicas == 3 && l.Escrita == 2 && l.Leitura == 2);

        Assert.True(maisFraco.AndaramParaTras > maisForte.AndaramParaTras);
        Assert.True(maisFraco.NaoViramAPropriaEscrita > maisForte.NaoViramAPropriaEscrita);
    }

    /// <summary>
    /// A frase do texto: o reparo de leitura nao e enfeite, ele muda o veredicto.
    /// </summary>
    [Fact]
    public void OReparoDeLeituraMudaOVeredicto()
    {
        var linhas = Medidas.Reparos();

        Assert.Equal(2, linhas.Count);

        var sem = linhas.Single(l => l.Jeito == "sem reparo");
        var com = linhas.Single(l => l.Jeito == "com reparo");

        Assert.True(com.Linearizaveis > sem.Linearizaveis);
        Assert.True(com.AndaramParaTras < sem.AndaramParaTras);
    }

    /// <summary>
    /// A frase do texto: com UM processo so, nao ha concorrencia e tudo e
    /// linearizavel. Com quatro, uma em cada dez historias nao e.
    /// </summary>
    [Fact]
    public void MaisConcorrenciaDaMenosHistoriasLinearizaveis()
    {
        var linhas = Medidas.Concorrencias();

        Assert.Equal(4, linhas.Count);

        var um = linhas.Single(l => l.Processos == 1);

        Assert.Equal(0, um.FracaoConcorrente, 9);
        Assert.Equal(um.Historias, um.Linearizaveis);

        for (var i = 1; i < linhas.Count; i++)
        {
            Assert.True(linhas[i].FracaoConcorrente > linhas[i - 1].FracaoConcorrente);
            Assert.True(linhas[i].Linearizaveis <= linhas[i - 1].Linearizaveis);
        }

        Assert.True(linhas[^1].Linearizaveis < linhas[^1].Historias);
    }
}
