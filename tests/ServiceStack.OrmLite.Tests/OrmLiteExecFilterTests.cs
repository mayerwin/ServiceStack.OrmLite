﻿using System;
using System.Data;
using System.Linq;
using NUnit.Framework;
using ServiceStack.Common.Tests.Models;
using ServiceStack.OrmLite.Tests.Shared;
using ServiceStack.Text;

namespace ServiceStack.OrmLite.Tests
{
    public class ReplayOrmLiteExecFilter : OrmLiteExecFilter
    {
        public int ReplayTimes { get; set; }

        public override T Exec<T>(IDbConnection dbConn, System.Func<IDbCommand, T> filter)
        {
            var holdProvider = OrmLiteConfig.DialectProvider;
            var dbCmd = CreateCommand(dbConn);
            try
            {
                var ret = default(T);
                for (var i = 0; i < ReplayTimes; i++)
                {
                    ret = filter(dbCmd);
                }
                return ret;
            }
            finally
            {
                DisposeCommand(dbCmd);
                OrmLiteConfig.DialectProvider = holdProvider;
            }
        }
    }

    public class MockStoredProcExecFilter : OrmLiteExecFilter
    {
        public override T Exec<T>(IDbConnection dbConn, System.Func<IDbCommand, T> filter)
        {
            try
            {
                return base.Exec(dbConn, filter);
            }
            catch (Exception ex)
            {
                var sql = dbConn.GetLastSql();
                if (sql == "exec sp_name @firstName, @age")
                {
                    return (T)(object)new Person { FirstName = "Mocked" };
                }
                throw;
            }
        }
    }

    [TestFixture]
    public class OrmLiteExecFilterTests
        : OrmLiteTestBase
    {
        [Test]
        public void Can_add_replay_logic()
        {
            var holdExecFilter = OrmLiteConfig.ExecFilter;
            OrmLiteConfig.ExecFilter = new ReplayOrmLiteExecFilter { ReplayTimes = 3 };

            using (var db = OpenDbConnection())
            {
                db.DropAndCreateTable<ModelWithIdAndName>();
                db.Insert(new ModelWithIdAndName { Name = "Multiplicity" });

                var rowsInserted = db.Count<ModelWithIdAndName>(q => q.Name == "Multiplicity");
                Assert.That(rowsInserted, Is.EqualTo(3));
            }

            OrmLiteConfig.ExecFilter = holdExecFilter;
        }

        [Test]
        public void Can_mock_store_procedure()
        {
            var holdExecFilter = OrmLiteConfig.ExecFilter;
            OrmLiteConfig.ExecFilter = new MockStoredProcExecFilter();

            using (var db = OpenDbConnection())
            {
                var person = db.SqlScalar<Person>("exec sp_name @firstName, @age",
                    new { firstName = "aName", age = 1 });

                Assert.That(person.FirstName, Is.EqualTo("Mocked"));
            }

            OrmLiteConfig.ExecFilter = holdExecFilter;
        }
    }
}