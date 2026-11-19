using Npgsql;
using Sses.Api.Comun;

namespace Sses.Api.Tests;

public class CadenaConexionTests
{
    [Fact]
    public void Convierte_la_url_de_postgres_al_formato_de_Npgsql()
    {
        var b = new NpgsqlConnectionStringBuilder(
            CadenaConexion.Normalizar("postgres://sses:cl%40ve@dpg-abc-a:5433/sses_db?sslmode=require"));

        Assert.Equal("dpg-abc-a", b.Host);
        Assert.Equal(5433, b.Port);
        Assert.Equal("sses_db", b.Database);
        Assert.Equal("sses", b.Username);
        Assert.Equal("cl@ve", b.Password);
        Assert.Equal(SslMode.Require, b.SslMode);
    }

    [Fact]
    public void Sin_puerto_usa_5432()
    {
        var b = new NpgsqlConnectionStringBuilder(CadenaConexion.Normalizar("postgresql://u:p@host/db"));
        Assert.Equal(5432, b.Port);
    }

    [Fact]
    public void Deja_igual_la_cadena_de_Npgsql()
    {
        const string cadena = "Host=db;Port=5432;Database=sses;Username=sses;Password=x";
        Assert.Equal(cadena, CadenaConexion.Normalizar(cadena));
    }
}
