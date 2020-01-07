// --------------------------------------------------------------------------------------------------------------------
// <copyright file="SampleApp.cs" company="RHEA System S.A.">
//    Copyright (c) 2015-2020 RHEA System S.A.
// </copyright>
// --------------------------------------------------------------------------------------------------------------------

namespace CDP4.SDK.Samples
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Net.Http;
    using System.Net.Http.Headers;
    using System.Text;
    using System.Threading.Tasks;
    using CDP4Common.EngineeringModelData;
    using CDP4Common.SiteDirectoryData;
    using CDP4Common.Types;
    using CDP4Dal;
    using CDP4Dal.DAL;
    using CDP4Dal.Operations;
    using CDP4ServicesDal;
    using NLog;
    using NLog.Config;
    using NLog.Targets;

    public class SampleApp
    {
        private static ISession _session;
        private static Credentials _credentials;
        private static bool _isRunning = true;
        private static Uri _uri;

        // Commands
        private const string Open = "open";
        private const string Refresh = "refresh";
        private const string Reload = "reload";
        private const string Close = "close";
        private const string Restore = "restore";
        private const string GetIteration = "get_iteration";
        private const string PostPerson = "post_person";
        private const string PostParameter = "post_parameter";
        private const string PostPfsl = "post_pfsl";
        private const string PostPfslReorder = "post_pfsl_reorder";
        private const string RemoveParameter = "remove_parameter";

        static void Main(string[] args)
        {
            // Setup NLog
            var config = new LoggingConfiguration();

            // Targets where to log to: Console
            var logConsole = new ConsoleTarget("logConsole");

            // Rules for mapping loggers to targets            
            config.AddRule(LogLevel.Trace, LogLevel.Fatal, logConsole);

            // Apply config           
            LogManager.Configuration = config;

            printSeparator();
            Console.WriteLine("Welcome to CDP4 SDK sample app!");
            printSeparator();

            Console.WriteLine("Enter your user name (default is admin, just press Enter):");
            var userName = String.IsNullOrWhiteSpace(Console.ReadLine()) ? "admin" : Console.ReadLine();

            Console.WriteLine("Enter your password (to use default just press Enter):");
            var pass = String.IsNullOrWhiteSpace(Console.ReadLine()) ? "pass" : Console.ReadLine();

            Console.WriteLine(
                "Enter a server's URL for future requests (default is https://cdp4services-test.rheagroup.com, just press Enter):");
            _uri = new Uri(String.IsNullOrWhiteSpace(Console.ReadLine())
                ? "https://cdp4services-test.rheagroup.com"
                : Console.ReadLine());

            var dal = new CdpServicesDal();
            _credentials = new Credentials(userName, pass, _uri, null);
            _session = new Session(dal, _credentials);

            printCommands();

            while (_isRunning)
            {
                try
                {
                    executeCommand(Console.ReadLine());
                }
                catch (Exception ex)
                {
                    printSeparator();
                    Console.WriteLine("Something went wrong. Sorry about that.");
                    Console.WriteLine(ex.Message);
                    printSeparator();
                }
            }
        }

        private static void executeCommand(string command)
        {
            var args = command.Trim().Split(" ");
            switch (args[0])
            {
                case Open:
                {
                    open();
                    break;
                }
                case Refresh:
                {
                    refresh();
                    break;
                }
                case Reload:
                {
                    reload();
                    break;
                }
                case GetIteration:
                {
                    getIteration();
                    break;
                }
                case PostPerson:
                {
                    postPerson();
                    break;
                }
                case PostParameter:
                {
                    postParameter();
                    break;
                }
                case PostPfsl:
                {
                    postPossibleFiniteStateList();
                    break;
                }
                case PostPfslReorder:
                {
                    postPossibleFiniteStateListReorder();
                    break;
                }
                case RemoveParameter:
                {
                    removeParameter();
                    break;
                }
                case Close:
                {
                    close();
                    break;
                }
                case Restore:
                {
                    restore();
                    break;
                }
                default:
                {
                    Console.WriteLine("Unrecognized command.");
                    break;
                }
            }
        }

        private static async Task postPossibleFiniteStateListReorder()
        {
            if (_session.OpenIterations.Count == 0)
            {
                Console.WriteLine("At first an iteration should be opened");
                return;
            }

            var iteration = _session.OpenIterations.Keys.First();
            if (iteration != null)
            {
                var iterationClone = iteration.Clone(false);
                var pfsl = iteration.PossibleFiniteStateList.First(x => x.Name.Equals("PossibleFiniteStateList1"));

                if (pfsl == null)
                {
                    Console.WriteLine("There is not a predefined PossibleFiniteStateList. Execute post_pfsl");
                    return;
                }

                var pfslClone = pfsl.Clone(true);

                // make sure keys are preserved
                var itemsMap = new Dictionary<object, long>();
                pfsl.PossibleState.ToDtoOrderedItemList()
                    .ToList().ForEach(x => itemsMap.Add(x.V, x.K));
                var orderedItems = new List<OrderedItem>();
                pfslClone.PossibleState.SortedItems.Values.ToList().ForEach(x =>
                {
                    itemsMap.TryGetValue(x.Iid, out var value);
                    var orderedItem = new OrderedItem() {K = value, V = x};
                    orderedItems.Add(orderedItem);
                });

                pfslClone.PossibleState.Clear();
                pfslClone.PossibleState.AddOrderedItems(orderedItems);
                pfslClone.ModifiedOn = DateTime.Now;

                pfslClone.PossibleState.Move(1, 0);
                var transaction = new ThingTransaction(
                    TransactionContextResolver.ResolveContext(iterationClone),
                    iterationClone);
                transaction.CreateOrUpdate(pfslClone);

                await _session.Write(transaction.FinalizeTransaction());

                printCacheCount();

                printCommands();
            }
        }

        private static async Task postPossibleFiniteStateList()
        {
            if (_session.OpenIterations.Count == 0)
            {
                Console.WriteLine("At first an iteration should be opened");
                return;
            }

            var iteration = _session.OpenIterations.Keys.First();
            if (iteration != null)
            {
                var iterationClone = iteration.Clone(false);
                var pfs1 = new PossibleFiniteState(Guid.NewGuid(), _session.Assembler.Cache, _uri)
                    {Name = "state1", ShortName = "s1"};

                var pfs2 = new PossibleFiniteState(Guid.NewGuid(), _session.Assembler.Cache, _uri)
                    {Name = "state2", ShortName = "s2"};

                var pfsList = new PossibleFiniteStateList(Guid.NewGuid(),
                    _session.Assembler.Cache, _uri) {Name = "PossibleFiniteStateList1", ShortName = "PFSL1"};

                _session.OpenIterations.TryGetValue(iteration, out var tuple);
                var domainOfExpertise = tuple.Item1;
                pfsList.Owner = domainOfExpertise;

                var transaction = new ThingTransaction(
                    TransactionContextResolver.ResolveContext(iterationClone),
                    iterationClone);
                transaction.Create(pfsList, iterationClone);
                transaction.Create(pfs1, pfsList);
                transaction.Create(pfs2, pfsList);

                await _session.Write(transaction.FinalizeTransaction());

                printCacheCount();

                printCommands();
            }
        }

        private static async Task postParameter()
        {
            if (_session.OpenIterations.Count == 0)
            {
                Console.WriteLine("At first an iteration should be opened");
                return;
            }

            var iteration = _session.OpenIterations.Keys.First();
            if (iteration != null)
            {
                var elementDefinition = iteration.Element[0];
                var elementDefinitionClone = elementDefinition.Clone(false);
                _session.OpenIterations.TryGetValue(iteration, out var tuple);
                var domainOfExpertise = tuple.Item1;

                var parameter = new Parameter(Guid.NewGuid(), _session.Assembler.Cache, _uri);
                parameter.ParameterType =
                    _session.Assembler.Cache.Values.Select(x => x.Value).OfType<ParameterType>().First();
                parameter.Owner = domainOfExpertise;

                var transaction = new ThingTransaction(
                    TransactionContextResolver.ResolveContext(elementDefinitionClone),
                    elementDefinitionClone);
                transaction.Create(parameter, elementDefinitionClone);

                await _session.Write(transaction.FinalizeTransaction());

                printCacheCount();

                printCommands();
            }
        }

        private static void printSeparator()
        {
            Console.WriteLine("*********************************");
        }

        private static async Task open()
        {
            await _session.Open();
            printCacheCount();

            printCommands();
        }

        private static async Task refresh()
        {
            if (isSiteDirectoryUnavailable())
            {
                Console.WriteLine("At first a connection should be opened.");
                return;
            }

            await _session.Refresh();
            printCacheCount();

            printCommands();
        }

        private static async Task reload()
        {
            if (isSiteDirectoryUnavailable())
            {
                Console.WriteLine("At first a connection should be opened.");
                return;
            }

            await _session.Reload();
            printCacheCount();

            printCommands();
        }

        private static async Task close()
        {
            if (isSiteDirectoryUnavailable())
            {
                Console.WriteLine("At first a connection should be opened.");
                return;
            }

            try
            {
                await _session.Close();
            }
            catch (Exception ex)
            {
                Console.WriteLine("During close operation an error is received: ");
                Console.WriteLine(ex.Message);
            }

            printSeparator();
            Console.WriteLine("Good bye!");
            printSeparator();
            _isRunning = false;
        }

        private static void restore()
        {
            if (!isSiteDirectoryUnavailable())
            {
                Console.WriteLine("It is possible to restore the server only before connection is opened.");
                return;
            }

            var uriBuilder = new UriBuilder(_uri);
            uriBuilder.Path = "/Data/Restore";

            var client = new HttpClient();
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic",
                Convert.ToBase64String(ASCIIEncoding.ASCII.GetBytes(string.Format("{0}:{1}", _credentials.UserName,
                    _credentials.Password))));

            client.PostAsync(uriBuilder.Uri, null);
            client.Dispose();

            printCommands();
        }

        private static async Task removeParameter()
        {
            if (_session.OpenIterations.Count == 0)
            {
                Console.WriteLine("At first an iteration should be opened");
                return;
            }

            var iteration = _session.OpenIterations.Keys.First();
            if (iteration != null)
            {
                var elementDefinition = iteration.Element[0];
                var elementDefinitionClone = elementDefinition.Clone(false);
                var parameterClone = elementDefinition.Parameter[0].Clone(false);

                var transaction = new ThingTransaction(
                    TransactionContextResolver.ResolveContext(elementDefinitionClone),
                    elementDefinitionClone);
                transaction.Delete(parameterClone, elementDefinitionClone);

                await _session.Write(transaction.FinalizeTransaction());

                printCacheCount();

                printCommands();
            }
        }

        private static async Task getIteration()
        {
            var siteDirectory = _session.Assembler.RetrieveSiteDirectory();
            if (isSiteDirectoryUnavailable())
            {
                Console.WriteLine("At first a connection should be opened.");
                return;
            }

            var engineeringModelIid = siteDirectory.Model[0].EngineeringModelIid;
            var iterationIid = siteDirectory.Model[0].IterationSetup[0].IterationIid;
            var domainOfExpertiseIid = siteDirectory.Model[0].ActiveDomain[0].Iid;

            var model = new EngineeringModel(engineeringModelIid, _session.Assembler.Cache, _uri);
            var iteration = new Iteration(iterationIid, _session.Assembler.Cache, _uri);
            iteration.Container = model;
            var domainOfExpertise = new DomainOfExpertise(domainOfExpertiseIid,
                _session.Assembler.Cache, _uri);

            await _session.Read(iteration, domainOfExpertise);

            printCacheCount();

            printCommands();
        }

        private static async Task postPerson()
        {
            if (isSiteDirectoryUnavailable())
            {
                Console.WriteLine("At first a connection should be opened.");
                return;
            }

            // Create person object
            var person = new Person(Guid.NewGuid(), _session.Assembler.Cache, _uri)
            {
                IsActive = true, ShortName = "M" + DateTime.Now, Surname = "Mouse", GivenName = "Mike",
                Password = "password"
            };
            var email1 = new EmailAddress(Guid.NewGuid(), _session.Assembler.Cache, _uri)
                {Value = "mikki.home@mouse.com", VcardType = VcardEmailAddressKind.HOME};

            person.DefaultEmailAddress = email1;

            var email2 = new EmailAddress(Guid.NewGuid(), _session.Assembler.Cache, _uri)
                {Value = "mikki.work@mouse.com", VcardType = VcardEmailAddressKind.WORK};

            var modifiedSiteDirectory = _session.Assembler.RetrieveSiteDirectory().Clone(true);

            var transaction = new ThingTransaction(
                TransactionContextResolver.ResolveContext(modifiedSiteDirectory), modifiedSiteDirectory);
            transaction.Create(person, modifiedSiteDirectory);
            transaction.Create(email1, person);
            transaction.Create(email2, person);

            await _session.Write(transaction.FinalizeTransaction());

            printCacheCount();

            printCommands();
        }

        private static void printCacheCount()
        {
            Console.WriteLine(_session.Assembler.Cache.Count + " objects currently in the cache.");
        }

        private static void printCommands()
        {
            printSeparator();
            Console.WriteLine("Available commands:");
            Console.WriteLine(Open + " - Open a connection to a data-source");
            Console.WriteLine(Refresh + " - Update the Cache with updated Things from a data-source");
            Console.WriteLine(Reload + " - Reload all Things from a data-source for all open TopContainers");
            Console.WriteLine(
                Close
                + " - Close the connection to a data-source and clear the Cache and exits the program");
            Console.WriteLine(Restore + " - Restores the state of a data-source to its default state");
            Console.WriteLine(
                GetIteration
                + " - gets a predefined iteration of an engineering model with dependent objects");
            Console.WriteLine(PostPerson + " - posts a predefined person with 2 e-mail addresses");
            Console.WriteLine(PostParameter + " - posts a predefined parameter");
            Console.WriteLine(
                PostPfsl + " - posts a predefined PossibleFiniteStateList with 2 PossibleFiniteStates");
            Console.WriteLine(
                PostPfslReorder
                + " - reorders(rotates in this particular case) states in the created predefined PossibleFiniteStateList (post_pfsl)");
            Console.WriteLine(RemoveParameter + " - removes a predefined Parameter of ElementDefinition");
            printSeparator();
        }

        private static bool isSiteDirectoryUnavailable()
        {
            return _session.Assembler.RetrieveSiteDirectory() == null;
        }
    }
}