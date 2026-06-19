using Dapper;
using NkplmErp.Shared.Repositories.Interface;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using NkplmErp.Shared.Models.ResponseModel;
using System.Data;
using Microsoft.Data.SqlClient;

namespace NkplmErp.Shared.Repositories.Implementation;

public class DapperRepository : IDapperRepository
{
    private readonly string _connectionString;
    private readonly ILogger<DapperRepository> _logger;
    private readonly IConfiguration _configuration;
    public DapperRepository(ILogger<DapperRepository> logger, IConfiguration configuration)
    {
        _configuration = configuration;
        this._connectionString = (Environment.GetEnvironmentVariable("DOCKER_RUNNING") == "TRUE") ? $"Data Source={Environment.GetEnvironmentVariable("DB_HOST")};Initial Catalog={Environment.GetEnvironmentVariable("DB_NAME")};User ID={Environment.GetEnvironmentVariable("DB_USER")};Password={Environment.GetEnvironmentVariable("DB_PASSWORD")};pooling='true';Max Pool Size=200000;TrustServerCertificate=True;"
            : _configuration.GetConnectionString("DefaultConnection")!;
        this._logger = logger;
    }
    /// <summary>
    /// Executes the query to get the results.
    /// </summary>
    /// <typeparam name="T">The type of result data.</typeparam>
    /// <param name="sqlQuery">The sql query.</param>
    /// <param name="sqlParam">The paramters.</param>
    /// <param name="queryType">The transaction.</param>
    public async Task<List<T>> GetQueryResultAsync<T>(string sqlQuery, object sqlParam, CommandType queryType)
    {
        //this._logger.LogDebug($"QUERY GetQueryResultAsync COMMAND | {sqlQuery}");
        //this._logger.LogDebug($"QUERY GetQueryResultAsync PARAMETERS | {sqlParam}");

        using var sqlConnection = new SqlConnection(_connectionString);
        {
            try
            {
                sqlConnection.Open();
                var command = new CommandDefinition(sqlQuery, sqlParam, commandType: queryType);
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

    /// <summary>
    /// Executes the query to get the results.
    /// </summary>
    /// <typeparam name="T">The type of result data.</typeparam>
    /// <param name="sqlQuery">The sql query.</param>
    /// <param name="sqlParam">The paramters.</param>
    /// <param name="queryType">The transaction.</param>
    public async Task<T> GetQueryFirstOrDefaultResultAsync<T>(string sqlQuery, object sqlParam, CommandType queryType)
    {
        _logger.LogDebug("QUERY ExecuteAsync COMMAND | {sqlQuery} {sqlParam}", sqlQuery, sqlParam);

        using var sqlConnection = new SqlConnection(_connectionString);
        {
            try
            {
                sqlConnection.Open();
                var command = new CommandDefinition(sqlQuery, sqlParam, commandType: queryType);
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

    /// <summary>
    /// Executes the query
    /// </summary>
    /// <param name="sqlQuery">The sql query.</param>
    /// <param name="sqlParam">The query parameters</param>
    /// <param name="queryType">The transaction.</param>
    /// <returns>The affected number of rows.</returns>
    public async Task<SystemResponse> ExecuteAsync(string sqlQuery, object sqlParam, CommandType queryType = CommandType.StoredProcedure)
    {
        this._logger.LogDebug("QUERY ExecuteAsync COMMAND | {sqlQuery} {sqlParam}", sqlQuery, sqlParam);

        using var sqlConnection = new SqlConnection(_connectionString);
        {
            try
            {
                sqlConnection.Open();
                var command = new CommandDefinition(sqlQuery, sqlParam, commandType: queryType);
                var result = await sqlConnection.QueryFirstOrDefaultAsync<SystemResponse>(command);
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
    /// <summary>
    /// Reads from multiple query.
    /// </summary>
    /// <param name="sqlQuery">The sql query</param>
    /// <param name="sqlParam">THe parameters.</param>
    /// <param name="queryType">The transaction.</param>
    /// <returns>The grid reader.</returns>
    public async Task<List<object>> GetFromMultipleQuery<T0, T1>(string sqlQuery, object sqlParam, CommandType queryType = CommandType.StoredProcedure)
    {
        // this._logger.LogDebug($"QUERY InsertAndGetId COMMAND | {sqlQuery}");
        // this._logger.LogDebug($"QUERY InsertAndGetId PARAMETERS | {sqlParam}");

        using var sqlConnection = new SqlConnection(_connectionString);
        {
            try
            {
                sqlConnection.Open();
                var command = new CommandDefinition(sqlQuery, sqlParam, commandType: queryType);
                var result = await sqlConnection.QueryMultipleAsync(command);
                var res = new List<object> { result.Read<T0>().ToList(), result.Read<T1>().ToList() };
                return res;
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
    public async Task<List<object>> GetFromMultipleQuery<T0, T1, T2>(string sqlQuery, object sqlParam, CommandType queryType = CommandType.StoredProcedure)
    {
        // this._logger.LogDebug($"QUERY InsertAndGetId COMMAND | {sqlQuery}");
        // this._logger.LogDebug($"QUERY InsertAndGetId PARAMETERS | {sqlParam}");

        using var sqlConnection = new SqlConnection(_connectionString);
        {
            try
            {
                sqlConnection.Open();
                var command = new CommandDefinition(sqlQuery, sqlParam, commandType: queryType);
                var result = await sqlConnection.QueryMultipleAsync(command);
                var res = new List<object> { result.Read<T0>().ToList(), result.Read<T1>().ToList(), result.Read<T2>().ToList() };
                return res;
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
    public async Task<List<object>> GetFromMultipleQuery<T0, T1, T2, T3>(string sqlQuery, object sqlParam, CommandType queryType = CommandType.StoredProcedure)
    {
        // this._logger.LogDebug($"QUERY InsertAndGetId COMMAND | {sqlQuery}");
        // this._logger.LogDebug($"QUERY InsertAndGetId PARAMETERS | {sqlParam}");
        using var sqlConnection = new SqlConnection(_connectionString);
        {
            try
            {
                sqlConnection.Open();
                var command = new CommandDefinition(sqlQuery, sqlParam, commandType: queryType);
                var result = await sqlConnection.QueryMultipleAsync(command);
                var res = new List<object> { result.Read<T0>().ToList(), result.Read<T1>().ToList(), result.Read<T2>().ToList(), result.Read<T3>().ToList() };
                return res;
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
}
