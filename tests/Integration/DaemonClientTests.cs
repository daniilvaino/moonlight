using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using Moonlight.Node;
using Xunit;

namespace Moonlight.Integration.Tests;

/// <summary>
/// The client against canned monerod answers. No network: what is under test is
/// the request we send and the answer we accept, not the daemon.
/// </summary>
public class DaemonClientTests
{
    [Fact]
    public async Task GetHeightReadsTheHeight()
    {
        using DaemonClient client = Client(_ => """{"height":3140000,"status":"OK"}""", out StubHandler stub);

        Assert.Equal(3_140_000UL, await client.GetHeightAsync());
        Assert.Equal("/get_height", stub.LastPath);
    }

    [Fact]
    public async Task GetBlockSendsJsonRpcAndParsesTheBlob()
    {
        // A minimal but real block: header, a coinbase, no other transactions.
        string blob = Convert.ToHexString(SampleBlockBlob()).ToLowerInvariant();

        using DaemonClient client = Client(_ => $$$"""
            {"id":"0","jsonrpc":"2.0","result":{
              "blob":"{{{blob}}}",
              "block_header":{"hash":"aa","height":9,"major_version":1,"minor_version":0,
                              "timestamp":1409804570,"prev_hash":"bb","nonce":7,"num_txes":0,"reward":1},
              "miner_tx_hash":"cc","tx_hashes":[]}}
            """, out StubHandler stub);

        Serialization.Block block = await client.GetBlockAsync(9);

        Assert.Equal("/json_rpc", stub.LastPath);
        Assert.Contains("\"method\":\"get_block\"", stub.LastBody, StringComparison.Ordinal);
        Assert.Contains("\"height\":9", stub.LastBody, StringComparison.Ordinal);
        Assert.Contains("\"jsonrpc\":\"2.0\"", stub.LastBody, StringComparison.Ordinal);

        Assert.True(block.MinerTransaction.IsCoinbase);
        Assert.Empty(block.TransactionIds);
    }

    [Fact]
    public async Task JsonRpcErrorsBecomeExceptions()
    {
        using DaemonClient client = Client(_ =>
            """{"id":"0","jsonrpc":"2.0","error":{"code":-2,"message":"Block not found"}}""", out _);

        DaemonException error = await Assert.ThrowsAsync<DaemonException>(() => client.GetBlockAsync(1));
        Assert.Contains("Block not found", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task TransactionsComeBackInTheOrderAsked()
    {
        using DaemonClient client = Client(_ => """
            {"status":"OK","txs":[
              {"tx_hash":"bb","as_hex":"0203"},
              {"tx_hash":"aa","as_hex":"0001"}]}
            """, out StubHandler stub);

        byte[][] blobs = await client.GetTransactionsAsync(["aa", "bb"]);

        Assert.Equal("/get_transactions", stub.LastPath);
        Assert.Equal([0x00, 0x01], blobs[0]);
        Assert.Equal([0x02, 0x03], blobs[1]);
    }

    [Fact]
    public async Task AMissingTransactionIsAnErrorRatherThanAGap()
    {
        using DaemonClient client = Client(_ =>
            """{"status":"OK","txs":[],"missed_tx":["aa"]}""", out _);

        await Assert.ThrowsAsync<DaemonException>(() => client.GetTransactionsAsync(["aa"]));
    }

    [Fact]
    public async Task HttpFailuresSurface()
    {
        using DaemonClient client = Client(_ => "", out _, HttpStatusCode.ServiceUnavailable);

        await Assert.ThrowsAsync<HttpRequestException>(() => client.GetHeightAsync());
    }

    private static DaemonClient Client(
        Func<HttpRequestMessage, string> respond,
        out StubHandler handler,
        HttpStatusCode status = HttpStatusCode.OK)
    {
        handler = new StubHandler(respond, status);
        return new DaemonClient(new HttpClient(handler) { BaseAddress = new Uri("http://node.invalid:18081/") });
    }

    /// <summary>Header, a coinbase transaction, and an empty transaction list.</summary>
    private static byte[] SampleBlockBlob()
    {
        Serialization.BlockHeader header = new(1, 0, 1_409_804_570,
            Convert.FromHexString("5da0a3d004c352a90cc86b00fab676695d76a4d1de16036c41ba4dd188c4d76f"), 7);

        byte[] coinbase =
        [
            0x02,                               // version 2
            0x00,                               // unlock time
            0x01, 0xFF, 0x09,                   // one input: txin_gen at height 9
            0x00,                               // no outputs
            0x00,                               // no extra
            0x00,                               // RingCT type: null
        ];

        return [.. header.Serialize(), .. coinbase, 0x00];
    }

    private sealed class StubHandler(Func<HttpRequestMessage, string> respond, HttpStatusCode status) : HttpMessageHandler
    {
        public string LastPath { get; private set; } = "";

        public string LastBody { get; private set; } = "";

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            LastPath = request.RequestUri!.AbsolutePath;
            LastBody = request.Content is null ? "" : await request.Content.ReadAsStringAsync(cancellationToken);

            return new HttpResponseMessage(status)
            {
                Content = new StringContent(respond(request), Encoding.UTF8, "application/json"),
            };
        }
    }
}
