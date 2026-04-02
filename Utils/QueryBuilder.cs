using System.Data.Common;

namespace CRM.Server.Utils
{
    public interface IQueryBuilderProvider
    {
        IQueryBuilder GetQueryBuilder(string queryString);
    }

    public class QueryBuilderProvider : IQueryBuilderProvider
    {
        public IQueryBuilder GetQueryBuilder(string queryString)
        {
            return new QueryBuilder(queryString);
        }
    }

    public interface IQueryBuilder
    {
        void AddOrderBy(QueryBuilder.Order order, string orderby);
        void AddGroupBy(string groupBy);
        void AddParameter(string columnName, string operatorSymbol, string parameterName, object parameterValue, DbTypes.Types parameterType);
        void AddParameter(string conditionString, string parameterName, object parameterValue, DbTypes.Types parameterType);
        void AddParameter(string conditionString);
        DbCommand GetCommand(IDb db);
        void AddLimitOffset(long limit, long offset);
    }

    public class QueryBuilder : IQueryBuilder
    {
        private string queryString;
        private string? orderBy;
        private string? order;
        private string? groupBy;
        private long limit;
        private long offset;
        private List<Parameter> parameterList = new();

        public QueryBuilder(string queryString)
        {
            this.queryString = queryString;
        }

        public void AddParameter(string columnName, string operatorSymbol, string parameterName, object parameterValue, DbTypes.Types parameterType)
        {
            var parameter = new Parameter();
            parameter.ConditionString = $"{columnName} {operatorSymbol} @{parameterName}";
            parameter.ParameterName = parameterName;
            parameter.ParameterValue = parameterValue;
            parameter.ParameterType = parameterType;
            parameterList.Add(parameter);
        }

        public void AddParameter(string conditionString, string parameterName, object parameterValue, DbTypes.Types parameterType)
        {
            var parameter = new Parameter();
            parameter.ConditionString = conditionString;
            parameter.ParameterName = parameterName;
            parameter.ParameterValue = parameterValue;
            parameter.ParameterType = parameterType;
            parameterList.Add(parameter);
        }

        public void AddParameter(string conditionString)
        {
            var parameter = new Parameter();
            parameter.ConditionString = conditionString;
            parameterList.Add(parameter);
        }

        public void AddOrderBy(Order order, string orderby)
        {
            this.orderBy = orderby;
            this.order = order.ToString();
        }

        public void AddGroupBy(string groupBy)
        {
            this.groupBy = groupBy;
        }

        public void AddLimitOffset(long limit, long offset)
        {
            this.limit = limit;
            this.offset = offset;
        }

        public DbCommand GetCommand(IDb db)
        {
            DbCommand command = db.GetCommand();

            bool isFirstElement = true;
            parameterList.ForEach(e =>
            {
                if (isFirstElement)
                {
                    isFirstElement = false;
                    queryString += $@" WHERE {e.ConditionString} ";
                }
                else
                {
                    queryString += $@" AND {e.ConditionString} ";
                }

                if (!string.IsNullOrEmpty(e.ParameterName))
                {
                    db.AddParameter(command, e.ParameterName, e.ParameterType).Value = e.ParameterValue;
                }
            });

            if (!string.IsNullOrEmpty(groupBy))
            {
                queryString += $" GROUP BY {groupBy} ";
            }

            if (!string.IsNullOrEmpty(order) && !string.IsNullOrEmpty(orderBy))
            {
                queryString += $" ORDER BY {orderBy} {order} ";
            }

            if (limit > 0)
            {
                queryString += $" LIMIT {limit}  OFFSET {offset} ";
            }

            command.CommandText = queryString;
            return command;
        }

        public class Parameter
        {
            public string ConditionString { get; set; } = string.Empty;
            public string ParameterName { get; set; } = string.Empty;
            public object ParameterValue { get; set; } = default!;
            public DbTypes.Types ParameterType { get; set; }
        }

        public enum Order
        {
            ASC,
            DESC
        }
    }
}

