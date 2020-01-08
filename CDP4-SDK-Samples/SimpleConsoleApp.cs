// --------------------------------------------------------------------------------------------------------------------
// <copyright file="SimpleConsoleApp.cs" company="RHEA System S.A.">
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

    public class SimpleConsoleApp
    {
        private ISession session;
        private Credentials credentials;
        private bool isRunning = true;
        private Uri uri;

        // Commands
        private const string openConnection = "open";
        private const string refreshData = "refresh";
        private const string reloadData = "reload";
        private const string closeConnection = "close";
        private const string restoreServer = "restore";
        private const string openIteration = "get_iteration";
        private const string postPredifinedPerson = "post_person";
        private const string postPredifinedParameter = "post_parameter";
        private const string postPfsl = "post_pfsl";
        private const string postPfslReorder = "post_pfsl_reorder";
        private const string removeParameterFromElementDefinition = "remove_parameter";

        public void Run()
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
            uri = new Uri(String.IsNullOrWhiteSpace(Console.ReadLine())
                ? "https://cdp4services-test.rheagroup.com"
                : Console.ReadLine());

            var dal = new CdpServicesDal();
            credentials = new Credentials(userName, pass, uri, null);
            session = new Session(dal, credentials);

            printCommands();

            while (isRunning)
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

        private void executeCommand(string command)
        {
            var args = command.Trim().Split(" ");
            switch (args[0])
            {
                case openConnection:
                {
                    open();
                    break;
                }
                case refreshData:
                {
                    refresh();
                    break;
                }
                case reloadData:
                {
                    reload();
                    break;
                }
                case openIteration:
                {
                    getIteration();
                    break;
                }
                case postPredifinedPerson:
                {
                    postPerson();
                    break;
                }
                case postPredifinedParameter:
                {
                    postParameter();
                    break;
                }
                case postPfsl:
                {
                    postPossibleFiniteStateList();
                    break;
                }
                case postPfslReorder:
                {
                    postPossibleFiniteStateListReorder();
                    break;
                }
                case removeParameterFromElementDefinition:
                {
                    removeParameter();
                    break;
                }
                case closeConnection:
                {
                    close();
                    break;
                }
                case restoreServer:
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

        private async Task postPossibleFiniteStateListReorder()
        {
            if (session.OpenIterations.Count == 0)
            {
                Console.WriteLine("At first an iteration should be opened");
                return;
            }

            var iteration = session.OpenIterations.Keys.First();
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

                await session.Write(transaction.FinalizeTransaction());

                printCacheCount();

                printCommands();
            }
        }

        private async Task postPossibleFiniteStateList()
        {
            if (session.OpenIterations.Count == 0)
            {
                Console.WriteLine("At first an iteration should be opened");
                return;
            }

            var iteration = session.OpenIterations.Keys.First();
            if (iteration != null)
            {
                var iterationClone = iteration.Clone(false);
                var pfs1 = new PossibleFiniteState(Guid.NewGuid(), session.Assembler.Cache, uri)
                    {Name = "state1", ShortName = "s1"};

                var pfs2 = new PossibleFiniteState(Guid.NewGuid(), session.Assembler.Cache, uri)
                    {Name = "state2", ShortName = "s2"};

                var pfsList = new PossibleFiniteStateList(Guid.NewGuid(),
                    session.Assembler.Cache, uri) {Name = "PossibleFiniteStateList1", ShortName = "PFSL1"};

                session.OpenIterations.TryGetValue(iteration, out var tuple);
                var domainOfExpertise = tuple.Item1;
                pfsList.Owner = domainOfExpertise;

                var transaction = new ThingTransaction(
                    TransactionContextResolver.ResolveContext(iterationClone),
                    iterationClone);
                transaction.Create(pfsList, iterationClone);
                transaction.Create(pfs1, pfsList);
                transaction.Create(pfs2, pfsList);

                await session.Write(transaction.FinalizeTransaction());

                printCacheCount();

                printCommands();
            }
        }

        private async Task postParameter()
        {
            if (session.OpenIterations.Count == 0)
            {
                Console.WriteLine("At first an iteration should be opened");
                return;
            }

            var iteration = session.OpenIterations.Keys.First();
            if (iteration != null)
            {
                var elementDefinition = iteration.Element[0];
                var elementDefinitionClone = elementDefinition.Clone(false);
                session.OpenIterations.TryGetValue(iteration, out var tuple);
                var domainOfExpertise = tuple.Item1;

                var parameter = new Parameter(Guid.NewGuid(), session.Assembler.Cache, uri);
                parameter.ParameterType =
                    session.Assembler.Cache.Values.Select(x => x.Value).OfType<ParameterType>().First();
                parameter.Owner = domainOfExpertise;

                var transaction = new ThingTransaction(
                    TransactionContextResolver.ResolveContext(elementDefinitionClone),
                    elementDefinitionClone);
                transaction.Create(parameter, elementDefinitionClone);

                await session.Write(transaction.FinalizeTransaction());

                printCacheCount();

                printCommands();
            }
        }

        private void printSeparator()
        {
            Console.WriteLine("*********************************");
        }

        private async Task open()
        {
            await session.Open();
            printCacheCount();

            printCommands();
        }

        private async Task refresh()
        {
            if (isSiteDirectoryUnavailable())
            {
                Console.WriteLine("At first a connection should be opened.");
                return;
            }

            await session.Refresh();
            printCacheCount();

            printCommands();
        }

        private async Task reload()
        {
            if (isSiteDirectoryUnavailable())
            {
                Console.WriteLine("At first a connection should be opened.");
                return;
            }

            await session.Reload();
            printCacheCount();

            printCommands();
        }

        private async Task close()
        {
            if (isSiteDirectoryUnavailable())
            {
                Console.WriteLine("At first a connection should be opened.");
                return;
            }

            try
            {
                await session.Close();
            }
            catch (Exception ex)
            {
                Console.WriteLine("During close operation an error is received: ");
                Console.WriteLine(ex.Message);
            }

            printSeparator();
            Console.WriteLine("Good bye!");
            printSeparator();
            isRunning = false;
        }

        private void restore()
        {
            if (!isSiteDirectoryUnavailable())
            {
                Console.WriteLine("It is possible to restore the server only before connection is opened.");
                return;
            }

            var uriBuilder = new UriBuilder(uri);
            uriBuilder.Path = "/Data/Restore";

            var client = new HttpClient();
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic",
                Convert.ToBase64String(ASCIIEncoding.ASCII.GetBytes(string.Format("{0}:{1}", credentials.UserName,
                    credentials.Password))));

            client.PostAsync(uriBuilder.Uri, null);
            client.Dispose();

            printCommands();
        }

        private async Task removeParameter()
        {
            if (session.OpenIterations.Count == 0)
            {
                Console.WriteLine("At first an iteration should be opened");
                return;
            }

            var iteration = session.OpenIterations.Keys.First();
            if (iteration != null)
            {
                var elementDefinition = iteration.Element[0];
                var elementDefinitionClone = elementDefinition.Clone(false);
                var parameterClone = elementDefinition.Parameter[0].Clone(false);

                var transaction = new ThingTransaction(
                    TransactionContextResolver.ResolveContext(elementDefinitionClone),
                    elementDefinitionClone);
                transaction.Delete(parameterClone, elementDefinitionClone);

                await session.Write(transaction.FinalizeTransaction());

                printCacheCount();

                printCommands();
            }
        }

        private async Task getIteration()
        {
            var siteDirectory = session.Assembler.RetrieveSiteDirectory();
            if (isSiteDirectoryUnavailable())
            {
                Console.WriteLine("At first a connection should be opened.");
                return;
            }

            var engineeringModelIid = siteDirectory.Model[0].EngineeringModelIid;
            var iterationIid = siteDirectory.Model[0].IterationSetup[0].IterationIid;
            var domainOfExpertiseIid = siteDirectory.Model[0].ActiveDomain[0].Iid;

            var model = new EngineeringModel(engineeringModelIid, session.Assembler.Cache, uri);
            var iteration = new Iteration(iterationIid, session.Assembler.Cache, uri);
            iteration.Container = model;
            var domainOfExpertise = new DomainOfExpertise(domainOfExpertiseIid,
                session.Assembler.Cache, uri);

            await session.Read(iteration, domainOfExpertise);

            printCacheCount();

            printCommands();
        }

        private async Task postPerson()
        {
            if (isSiteDirectoryUnavailable())
            {
                Console.WriteLine("At first a connection should be opened.");
                return;
            }

            // Create person object
            var person = new Person(Guid.NewGuid(), session.Assembler.Cache, uri)
            {
                IsActive = true, ShortName = "M" + DateTime.Now, Surname = "Mouse", GivenName = "Mike",
                Password = "password"
            };
            var email1 = new EmailAddress(Guid.NewGuid(), session.Assembler.Cache, uri)
                {Value = "mikki.home@mouse.com", VcardType = VcardEmailAddressKind.HOME};

            person.DefaultEmailAddress = email1;

            var email2 = new EmailAddress(Guid.NewGuid(), session.Assembler.Cache, uri)
                {Value = "mikki.work@mouse.com", VcardType = VcardEmailAddressKind.WORK};

            var modifiedSiteDirectory = session.Assembler.RetrieveSiteDirectory().Clone(true);

            var transaction = new ThingTransaction(
                TransactionContextResolver.ResolveContext(modifiedSiteDirectory), modifiedSiteDirectory);
            transaction.Create(person, modifiedSiteDirectory);
            transaction.Create(email1, person);
            transaction.Create(email2, person);

            await session.Write(transaction.FinalizeTransaction());

            printCacheCount();

            printCommands();
        }

        private void printCacheCount()
        {
            Console.WriteLine(session.Assembler.Cache.Count + " objects currently in the cache.");
        }

        private void printCommands()
        {
            printSeparator();
            Console.WriteLine("Available commands:");
            Console.WriteLine(openConnection + " - Open a connection to a data-source");
            Console.WriteLine(refreshData + " - Update the Cache with updated Things from a data-source");
            Console.WriteLine(reloadData + " - Reload all Things from a data-source for all open TopContainers");
            Console.WriteLine(
                closeConnection
                + " - Close the connection to a data-source and clear the Cache and exits the program");
            Console.WriteLine(restoreServer + " - Restores the state of a data-source to its default state");
            Console.WriteLine(
                openIteration
                + " - gets a predefined iteration of an engineering model with dependent objects");
            Console.WriteLine(postPredifinedPerson + " - posts a predefined person with 2 e-mail addresses");
            Console.WriteLine(postPredifinedParameter + " - posts a predefined parameter");
            Console.WriteLine(
                postPfsl + " - posts a predefined PossibleFiniteStateList with 2 PossibleFiniteStates");
            Console.WriteLine(
                postPfslReorder
                + " - reorders(rotates in this particular case) states in the created predefined PossibleFiniteStateList (post_pfsl)");
            Console.WriteLine(removeParameterFromElementDefinition +
                              " - removes a predefined Parameter of ElementDefinition");
            printSeparator();
        }

        private bool isSiteDirectoryUnavailable()
        {
            return session.Assembler.RetrieveSiteDirectory() == null;
        }
    }
}
