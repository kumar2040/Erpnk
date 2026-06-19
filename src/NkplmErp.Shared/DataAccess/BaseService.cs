using Dapper;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Data;
using Microsoft.Data.SqlClient;

namespace NkplmErp.Shared.Services;

public abstract class BaseService(ILogger logger, IConfiguration configuration)
{

    private readonly ILogger _logger = logger;
    private readonly IConfiguration _configuration = configuration;

    protected async Task<List<T>> GetQueryResultAsync<T>(string sQuery, object parameters = null!, IDbTransaction transaction = null!)
    {
        string ConnectionString = (Environment.GetEnvironmentVariable("DOCKER_RUNNING") == "TRUE") ? $"Data Source={Environment.GetEnvironmentVariable("DB_HOST")};Initial Catalog={Environment.GetEnvironmentVariable("DB_NAME")};User ID={Environment.GetEnvironmentVariable("DB_USER")};Password={Environment.GetEnvironmentVariable("DB_PASSWORD")};pooling='true';Max Pool Size=200000;TrustServerCertificate=True;"
                : _configuration.GetConnectionString("DefaultConnection")!;
        using var sqlConnection = new SqlConnection(ConnectionString);
        {
            try
            {
                sqlConnection.Open();
                var command = new CommandDefinition(sQuery, parameters, transaction);
                var result = await sqlConnection.QueryAsync<T>(command);
                return result.ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError("Error on Database Operation. Error Details As: {StackTrace}", ex.StackTrace);
                throw;
            }
            finally
            {
                sqlConnection.Close();
            }
        }
    }

    protected async Task<T> GetQueryFirstOrDefaultResultAsync<T>(string sQuery, object parameters, IDbTransaction transaction = null!)
    {
        string ConnectionString = (Environment.GetEnvironmentVariable("DOCKER_RUNNING") == "TRUE") ? $"Data Source={Environment.GetEnvironmentVariable("DB_HOST")};Initial Catalog={Environment.GetEnvironmentVariable("DB_NAME")};User ID={Environment.GetEnvironmentVariable("DB_USER")};Password={Environment.GetEnvironmentVariable("DB_PASSWORD")};pooling='true';Max Pool Size=200000;TrustServerCertificate=True;"
                : _configuration.GetConnectionString("DefaultConnection")!;
        using var sqlConnection = new SqlConnection(ConnectionString);
        {
            try
            {
                sqlConnection.Open();
                var command = new CommandDefinition(sQuery, parameters, transaction);
                var result = await sqlConnection.QueryFirstOrDefaultAsync<T>(command);
                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError("Error on Database Operation. Error Details As: {StackTrace}", ex.StackTrace);
                throw;
            }
            finally
            {
                sqlConnection.Close();
            }
        }
    }

    protected async Task<int> ExecuteAsync(string sQuery, object parameters, IDbTransaction transaction = null!)
    {
        string ConnectionString = (Environment.GetEnvironmentVariable("DOCKER_RUNNING") == "TRUE") ? $"Data Source={Environment.GetEnvironmentVariable("DB_HOST")};Initial Catalog={Environment.GetEnvironmentVariable("DB_NAME")};User ID={Environment.GetEnvironmentVariable("DB_USER")};Password={Environment.GetEnvironmentVariable("DB_PASSWORD")};pooling='true';Max Pool Size=200000;TrustServerCertificate=True;"
                : _configuration.GetConnectionString("DefaultConnection")!;
        using var sqlConnection = new SqlConnection(ConnectionString);
        {
            try
            {
                sqlConnection.Open();
                var command = new CommandDefinition(sQuery, parameters, transaction);
                var rowsAffected = await sqlConnection.ExecuteAsync(command);
                return rowsAffected;
            }
            catch (Exception ex)
            {
                _logger.LogError("Error on Database Operation. Error Details As: {StackTrace}", ex.StackTrace);
                throw;
            }
            finally
            {
                sqlConnection.Close();
            }
        }
    }

    protected async Task<DisposableGridReader> GetFromMultipleQuery(string sQuery, object parameters, IDbTransaction transaction = null!)
    {
        string ConnectionString = (Environment.GetEnvironmentVariable("DOCKER_RUNNING") == "TRUE") ? $"Data Source={Environment.GetEnvironmentVariable("DB_HOST")};Initial Catalog={Environment.GetEnvironmentVariable("DB_NAME")};User ID={Environment.GetEnvironmentVariable("DB_USER")};Password={Environment.GetEnvironmentVariable("DB_PASSWORD")};pooling='true';Max Pool Size=200000;TrustServerCertificate=True;"
                : _configuration.GetConnectionString("DefaultConnection")!;
        var sqlConnection = new SqlConnection(ConnectionString);

        var command = new CommandDefinition(sQuery, parameters, transaction);
        var result = await sqlConnection.QueryMultipleAsync(command);
        return new DisposableGridReader(sqlConnection, result);
    }

    /// <summary>
    /// Execute Sql Query for datatable
    /// </summary>
    /// <param name="sQuery"></param>
    /// <returns></returns>
    protected async Task<DataTable> Execute(string sQuery)
    {
        string ConnectionString = (Environment.GetEnvironmentVariable("DOCKER_RUNNING") == "TRUE") ? $"Data Source={Environment.GetEnvironmentVariable("DB_HOST")};Initial Catalog={Environment.GetEnvironmentVariable("DB_NAME")};User ID={Environment.GetEnvironmentVariable("DB_USER")};Password={Environment.GetEnvironmentVariable("DB_PASSWORD")};pooling='true';Max Pool Size=200000;TrustServerCertificate=True;"
                : _configuration.GetConnectionString("DefaultConnection")!;
        using var connection = new SqlConnection(ConnectionString);
        {
            connection.Open();
            var dt = new DataTable();
            var cmd = new SqlCommand(sQuery, connection) { CommandType = CommandType.Text, CommandTimeout = 0 };
            var dr = await cmd.ExecuteReaderAsync();
            dt.Load(dr);
            cmd.Dispose();
            connection.Close();
            return dt;
        }
    }
    /// <summary>
    /// for dataset
    /// </summary>
    /// <param name="sQuery"></param>
    /// <returns></returns>
    protected async Task<DataSet> ExecuteQuery(string sQuery)
    {
        string ConnectionString = (Environment.GetEnvironmentVariable("DOCKER_RUNNING") == "TRUE") ? $"Data Source={Environment.GetEnvironmentVariable("DB_HOST")};Initial Catalog={Environment.GetEnvironmentVariable("DB_NAME")};User ID={Environment.GetEnvironmentVariable("DB_USER")};Password={Environment.GetEnvironmentVariable("DB_PASSWORD")};pooling='true';Max Pool Size=200000;TrustServerCertificate=True;"
                : _configuration.GetConnectionString("DefaultConnection")!;
        using var connection = new SqlConnection(ConnectionString);
        {
            connection.Open();
            var cmd = new SqlCommand() { CommandText = sQuery, Connection = connection, CommandTimeout = 0 };
            var ds = new DataSet();
            var da = new SqlDataAdapter(cmd.CommandText, cmd.Connection) { SelectCommand = cmd };
            await Task.Run(() => da.Fill(ds));
            cmd.Dispose();
            da.Dispose();
            return ds;
        }
    }
}
