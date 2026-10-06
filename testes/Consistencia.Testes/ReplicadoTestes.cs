using Conde.Consistencia;
using Xunit;

namespace Conde.Consistencia.Testes;

public class ReplicadoTestes
{
    [Fact]
    public void AReplicaGuardaOQueFoiGravadoComAVersaoMaior()
    {
        var armazem = new Replicado(3);

        armazem.Gravar(0, 7, 5);
        Assert.Equal((7, 5L), armazem.Ler(0));

        // versao menor nao sobrescreve: e assim que a replica atrasada nao
        // estraga o que ja chegou
        armazem.Gravar(0, 9, 3);
        Assert.Equal((7, 5L), armazem.Ler(0));

        armazem.Gravar(0, 9, 8);
        Assert.Equal((9, 8L), armazem.Ler(0));
    }

    [Fact]
    public void AsVersoesSaoCrescentes()
    {
        var armazem = new Replicado(3);
        var anterior = 0L;

        for (var i = 0; i < 100; i++)
        {
            var versao = armazem.ProximaVersao();
            Assert.True(versao > anterior);
            anterior = versao;
        }
    }

    [Fact]
    public void ReplicasInvalidasSaoRecusadas()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new Replicado(0));
        Assert.Throws<ArgumentOutOfRangeException>(() => new Replicado(-2));
    }

    /// <summary>
    /// A mesma semente da a mesma historia: sem isso, nenhuma das tabelas seria
    /// comparavel entre dois sistemas.
    /// </summary>
    [Fact]
    public void AMesmaSementeDaAMesmaHistoria()
    {
        var pedidos = Execucao.Sortear(3, 4, 0.4, 9);

        var uma = Execucao.Rodar(pedidos, 3, 2, 2, 5);
        var outra = Execucao.Rodar(pedidos, 3, 2, 2, 5);

        Assert.Equal(uma.Operacoes, outra.Operacoes);
    }

    /// <summary>
    /// Toda operacao da historia tem um intervalo bem formado e aparece uma vez.
    /// </summary>
    [Fact]
    public void AHistoriaCobreTodosOsPedidosComIntervaloBemFormado()
    {
        for (ulong semente = 1; semente <= 100; semente++)
        {
            var pedidos = Execucao.Sortear(3, 4, 0.4, semente);
            var historia = Execucao.Rodar(pedidos, 3, 2, 2, semente);

            Assert.Equal(pedidos.Count, historia.Quantas);

            foreach (var operacao in historia.Operacoes)
            {
                Assert.True(operacao.Comeco >= 0);
                Assert.True(operacao.Fim > operacao.Comeco);
            }
        }
    }

    /// <summary>
    /// As operacoes de um mesmo processo NAO se cruzam: um cliente sincrono so
    /// comeca a proxima depois de a anterior terminar.
    /// </summary>
    [Fact]
    public void AsOperacoesDeUmProcessoNaoSeCruzam()
    {
        for (ulong semente = 1; semente <= 100; semente++)
        {
            var pedidos = Execucao.Sortear(3, 4, 0.4, semente);
            var historia = Execucao.Rodar(pedidos, 3, 2, 2, semente);

            for (var p = 0; p < historia.Processos; p++)
            {
                var fila = historia.Do(p);

                for (var i = 1; i < fila.Count; i++)
                    Assert.True(Historia.AconteceuAntes(fila[i - 1], fila[i]),
                        $"as operacoes {i - 1} e {i} do processo {p} se cruzaram");
            }
        }
    }

    /// <summary>
    /// Uma escrita toca EXATAMENTE W replicas e uma leitura le EXATAMENTE R: o
    /// intervalo de cada operacao tem o tamanho do quorum dela.
    /// </summary>
    [Fact]
    public void CadaOperacaoTocaOTamanhoDoQuorumDela()
    {
        foreach (var (n, w, r) in new[] { (3, 1, 1), (3, 2, 2), (5, 3, 3) })
            for (ulong semente = 1; semente <= 30; semente++)
            {
                var pedidos = Execucao.Sortear(3, 4, 0.4, semente);
                var historia = Execucao.Rodar(pedidos, n, w, r, semente);

                foreach (var operacao in historia.Operacoes)
                {
                    var passos = operacao.Fim - operacao.Comeco;
                    var esperado = operacao.Acao == Acao.Escrever ? w : r;

                    // o intervalo tambem engloba os passos de outras operacoes que
                    // se intercalaram, entao ele nunca e menor que o quorum
                    Assert.True(passos >= esperado, $"{operacao} gastou {passos} passos para um quorum de {esperado}");
                }
            }
    }

    /// <summary>
    /// Os valores escritos sao todos DIFERENTES, de proposito.
    ///
    /// Com valores repetidos, uma violacao de consistencia pode ficar invisivel: a
    /// leitura devolve o valor certo pelo motivo errado, e a medida vira otimismo.
    /// </summary>
    [Fact]
    public void OsValoresEscritosSaoTodosDiferentes()
    {
        for (ulong semente = 1; semente <= 50; semente++)
        {
            var pedidos = Execucao.Sortear(3, 5, 0.5, semente);
            var escritos = pedidos.Where(p => p.Acao == Acao.Escrever).Select(p => p.Argumento).ToList();

            Assert.Equal(escritos.Count, escritos.Distinct().Count());
            Assert.All(escritos, v => Assert.True(v > 0, "o valor inicial nao pode ser escrito de novo"));
        }
    }

    /// <summary>
    /// Com UMA replica so, nao ha o que divergir: toda historia e linearizavel.
    ///
    /// E o controle do experimento: se isto falhasse, o problema estaria no modelo
    /// e nao na replicacao.
    /// </summary>
    [Fact]
    public void ComUmaReplicaSoTudoEhLinearizavel()
    {
        var especificacao = Especificacao.Registrador();

        for (ulong semente = 1; semente <= 200; semente++)
        {
            var pedidos = Execucao.Sortear(3, 4, 0.4, semente);
            var historia = Execucao.Rodar(pedidos, 1, 1, 1, semente);

            Assert.True(Linearizavel.Eh(historia, especificacao), $"semente {semente}");
        }
    }

    /// <summary>
    /// E o REPARO DE LEITURA deixa as replicas mais parecidas entre si: ele nao e
    /// enfeite.
    /// </summary>
    [Fact]
    public void OReparoDeLeituraMelhoraOResultado()
    {
        var especificacao = Especificacao.Registrador();

        var semReparo = 0;
        var comReparo = 0;

        for (ulong semente = 1; semente <= 200; semente++)
        {
            var pedidos = Execucao.Sortear(3, 4, 0.4, semente);

            if (Linearizavel.Eh(Execucao.Rodar(pedidos, 3, 2, 2, semente * 7 + 1), especificacao)) semReparo++;
            if (Linearizavel.Eh(Execucao.Rodar(pedidos, 3, 2, 2, semente * 7 + 1, comReparo: true), especificacao)) comReparo++;
        }

        Assert.True(comReparo > semReparo, $"sem reparo {semReparo}, com reparo {comReparo}");
    }
}
