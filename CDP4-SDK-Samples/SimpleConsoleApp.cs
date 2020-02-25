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
        // Commands
        private const string OpenConnection = "open";
        private const string RefreshData = "refresh";
        private const string ReloadData = "reload";
        private const string CloseConnection = "close";
        private const string RestoreServer = "restore";
        private const string OpenIteration = "get_iteration";
        private const string PostPredifinedPerson = "post_person";
        private const string PostPredifinedParameter = "post_parameter";
        private const string PostPfsl = "post_pfsl";
        private const string PostPfslReorder = "post_pfsl_reorder";
        private const string RemoveParameterFromElementDefinition = "remove_parameter";

        /// <summary>
        /// The <see cref="Credentials"/> that are used to connect the data-store
        /// </summary>
        private Credentials credentials;

        /// <summary>
        /// A flag signalling whether this app is still running
        /// </summary>
        private bool isRunning = true;

        /// <summary>
        /// The <see cref="ISession"/> that is used to communicate with the data-store
        /// </summary>
        private ISession session;

        /// <summary>
        /// The <see cref="Uri"/> of the data-store to connect to
        /// </summary>
        private Uri uri;

        /// <summary>
        /// Runs this console app
        /// </summary>
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

            this.PrintSeparator();
            Console.WriteLine("Welcome to CDP4 SDK sample app!");
            this.PrintSeparator();

            Console.WriteLine("Enter your user name (default is admin, just press Enter):");
            var userName = string.IsNullOrWhiteSpace(Console.ReadLine()) ? "admin" : Console.ReadLine();

            Console.WriteLine("Enter your password (to use default just press Enter):");
            var pass = string.IsNullOrWhiteSpace(Console.ReadLine()) ? "pass" : Console.ReadLine();

            Console.WriteLine(
                "Enter a server's URL for future requests (default is https://cdp4services-test.cdp4.org, just press Enter):");
            this.uri = new Uri(string.IsNullOrWhiteSpace(Console.ReadLine())
                ? "https://cdp4services-test.cdp4.org"
                : Console.ReadLine());

            var dal = new CdpServicesDal();
            this.credentials = new Credentials(userName, pass, this.uri);
            this.session = new Session(dal, this.credentials);

            this.PrintCommands();

            while (this.isRunning)
                try
                {
                    this.ExecuteCommand(Console.ReadLine());
                }
                catch (Exception ex)
                {
                    this.PrintSeparator();
                    Console.WriteLine("Something went wrong. Sorry about that.");
                    Console.WriteLine(ex.Message);
                    this.PrintSeparator();
                }
        }

        /// <summary>
        /// Executes a supplied command
        /// <param name="command">Name of a command to execute</param>
        /// </summary>
        private void ExecuteCommand(string command)
        {
            var args = command.Trim().Split(" ");
            switch (args[0])
            {
                case OpenConnection:
                {
                    this.Open();
                    break;
                }
                case RefreshData:
                {
                    this.Refresh();
                    break;
                }
                case ReloadData:
                {
                    this.Reload();
                    break;
                }
                case OpenIteration:
                {
                    this.GetIteration();
                    break;
                }
                case PostPredifinedPerson:
                {
                    this.PostPerson();
                    break;
                }
                case PostPredifinedParameter:
                {
                    this.PostParameter();
                    break;
                }
                case PostPfsl:
                {
                    this.PostPossibleFiniteStateList();
                    break;
                }
                case PostPfslReorder:
                {
                    this.PostPossibleFiniteStateListReorder();
                    break;
                }
                case RemoveParameterFromElementDefinition:
                {
                    this.RemoveParameter();
                    break;
                }
                case CloseConnection:
                {
                    this.Close();
                    break;
                }
                case RestoreServer:
                {
                    this.Restore();
                    break;
                }
                default:
                {
                    Console.WriteLine("Unrecognized command.");
                    break;
                }
            }
        }

        /// <summary>
        /// Posts a reorder of elements in <see cref="PossibleFiniteStateList"/>
        /// </summary>
        private void PostPossibleFiniteStateListReorder()
        {
            if (this.session.OpenIterations.Count == 0)
            {
                Console.WriteLine("At first an iteration should be opened");
                return;
            }

            var iteration = this.session.OpenIterations.Keys.First();
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
                    var orderedItem = new OrderedItem {K = value, V = x};
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

                this.session.Write(transaction.FinalizeTransaction()).GetAwaiter().GetResult();

                this.PrintCacheCount();

                this.PrintCommands();
            }
        }

        /// <summary>
        /// Posts a predefined <see cref="PossibleFiniteStateList"/>
        /// </summary>
        private void PostPossibleFiniteStateList()
        {
            if (this.session.OpenIterations.Count == 0)
            {
                Console.WriteLine("At first an iteration should be opened");
                return;
            }

            var iteration = this.session.OpenIterations.Keys.First();
            if (iteration != null)
            {
                var iterationClone = iteration.Clone(false);
                var pfs1 = new PossibleFiniteState(Guid.NewGuid(), this.session.Assembler.Cache, this.uri)
                    {Name = "state1", ShortName = "s1"};

                var pfs2 = new PossibleFiniteState(Guid.NewGuid(), this.session.Assembler.Cache, this.uri)
                    {Name = "state2", ShortName = "s2"};

                var pfsList = new PossibleFiniteStateList(Guid.NewGuid(), this.session.Assembler.Cache, this.uri)
                    {Name = "PossibleFiniteStateList1", ShortName = "PFSL1"};

                this.session.OpenIterations.TryGetValue(iteration, out var tuple);
                var domainOfExpertise = tuple.Item1;
                pfsList.Owner = domainOfExpertise;

                var transaction = new ThingTransaction(
                    TransactionContextResolver.ResolveContext(iterationClone),
                    iterationClone);
                transaction.Create(pfsList, iterationClone);
                transaction.Create(pfs1, pfsList);
                transaction.Create(pfs2, pfsList);

                this.session.Write(transaction.FinalizeTransaction()).GetAwaiter().GetResult();

                this.PrintCacheCount();

                this.PrintCommands();
            }
        }

        /// <summary>
        /// Posts a predefined <see cref="Parameter"/>
        /// </summary>
        private void PostParameter()
        {
            if (this.session.OpenIterations.Count == 0)
            {
                Console.WriteLine("At first an iteration should be opened");
                return;
            }

            var iteration = this.session.OpenIterations.Keys.First();
            if (iteration != null)
            {
                var elementDefinition = iteration.Element[0];
                var elementDefinitionClone = elementDefinition.Clone(false);
                this.session.OpenIterations.TryGetValue(iteration, out var tuple);
                var domainOfExpertise = tuple.Item1;

                var parameter = new Parameter(Guid.NewGuid(), this.session.Assembler.Cache, this.uri);
                parameter.ParameterType = this.session.Assembler.Cache.Values.Select(x => x.Value)
                    .OfType<ParameterType>().First();
                parameter.Owner = domainOfExpertise;

                var transaction = new ThingTransaction(
                    TransactionContextResolver.ResolveContext(elementDefinitionClone),
                    elementDefinitionClone);
                transaction.Create(parameter, elementDefinitionClone);

                this.session.Write(transaction.FinalizeTransaction()).GetAwaiter().GetResult();

                this.PrintCacheCount();

                this.PrintCommands();
            }
        }

        /// <summary>
        /// Prints a text separator to the console
        /// </summary>
        private void PrintSeparator()
        {
            Console.WriteLine("*********************************");
        }

        /// <summary>
        /// Opens a connection to a data-source specified by <see cref="uri"/>
        /// </summary>
        private void Open()
        {
            this.session.Open().GetAwaiter().GetResult();
            this.PrintCacheCount();

            this.PrintCommands();
        }

        /// <summary>
        /// Refreshes data to be in sync with the data-source
        /// </summary>
        private void Refresh()
        {
            if (this.IsSiteDirectoryUnavailable())
            {
                Console.WriteLine("At first a connection should be opened.");
                return;
            }

            this.session.Refresh().GetAwaiter().GetResult();
            this.PrintCacheCount();

            this.PrintCommands();
        }

        /// <summary>
        /// Reloads data from the data-source
        /// </summary>
        private void Reload()
        {
            if (this.IsSiteDirectoryUnavailable())
            {
                Console.WriteLine("At first a connection should be opened.");
                return;
            }

            this.session.Reload().GetAwaiter().GetResult();
            this.PrintCacheCount();

            this.PrintCommands();
        }

        /// <summary>
        /// Closes connection to the data-source and end the execution of this app
        /// </summary>
        private void Close()
        {
            if (this.IsSiteDirectoryUnavailable())
            {
                Console.WriteLine("At first a connection should be opened.");
                return;
            }

            try
            {
                this.session.Close().GetAwaiter().GetResult();
            }
            catch (Exception ex)
            {
                Console.WriteLine("During close operation an error is received: ");
                Console.WriteLine(ex.Message);
            }

            this.PrintSeparator();
            Console.WriteLine("Good bye!");
            this.PrintSeparator();
            this.isRunning = false;
        }

        /// <summary>
        /// Restores data on the data-source
        /// </summary>
        private void Restore()
        {
            if (!this.IsSiteDirectoryUnavailable())
            {
                Console.WriteLine("It is possible to restore the server only before connection is opened.");
                return;
            }

            var uriBuilder = new UriBuilder(this.uri);
            uriBuilder.Path = "/Data/Restore";

            var client = new HttpClient();
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic",
                Convert.ToBase64String(Encoding.ASCII.GetBytes(string.Format("{0}:{1}", this.credentials.UserName,
                    this.credentials.Password))));

            client.PostAsync(uriBuilder.Uri, null);
            client.Dispose();

            this.PrintCommands();
        }

        /// <summary>
        /// Removes the first found <see cref="Parameter"/> from the first found <see cref="ElementDefinition"/>
        /// </summary>
        private void RemoveParameter()
        {
            if (this.session.OpenIterations.Count == 0)
            {
                Console.WriteLine("At first an iteration should be opened");
                return;
            }

            var iteration = this.session.OpenIterations.Keys.First();
            if (iteration != null)
            {
                var elementDefinition = iteration.Element[0];
                var elementDefinitionClone = elementDefinition.Clone(false);
                var parameterClone = elementDefinition.Parameter[0].Clone(false);

                var transaction = new ThingTransaction(
                    TransactionContextResolver.ResolveContext(elementDefinitionClone),
                    elementDefinitionClone);
                transaction.Delete(parameterClone, elementDefinitionClone);

                this.session.Write(transaction.FinalizeTransaction()).GetAwaiter().GetResult();

                this.PrintCacheCount();

                this.PrintCommands();
            }
        }

        /// <summary>
        /// Retrieves <see cref="Iteration"/> related data of the first found <see cref="Iteration"/>
        /// </summary>
        private void GetIteration()
        {
            var siteDirectory = this.session.Assembler.RetrieveSiteDirectory();
            if (this.IsSiteDirectoryUnavailable())
            {
                Console.WriteLine("At first a connection should be opened.");
                return;
            }

            var engineeringModelIid = siteDirectory.Model[0].EngineeringModelIid;
            var iterationIid = siteDirectory.Model[0].IterationSetup[0].IterationIid;
            var domainOfExpertiseIid = siteDirectory.Model[0].ActiveDomain[0].Iid;

            var model = new EngineeringModel(engineeringModelIid, this.session.Assembler.Cache, this.uri);
            var iteration = new Iteration(iterationIid, this.session.Assembler.Cache, this.uri);
            iteration.Container = model;
            var domainOfExpertise = new DomainOfExpertise(domainOfExpertiseIid, this.session.Assembler.Cache, this.uri);

            this.session.Read(iteration, domainOfExpertise).GetAwaiter().GetResult();

            this.PrintCacheCount();

            this.PrintCommands();
        }

        /// <summary>
        /// Posts a predefined <see cref="Person"/>
        /// </summary>
        private void PostPerson()
        {
            if (this.IsSiteDirectoryUnavailable())
            {
                Console.WriteLine("At first a connection should be opened.");
                return;
            }

            // Create person object
            var person = new Person(Guid.NewGuid(), this.session.Assembler.Cache, this.uri)
            {
                IsActive = true, ShortName = "M" + DateTime.Now, Surname = "Mouse", GivenName = "Mike",
                Password = "password"
            };
            var email1 = new EmailAddress(Guid.NewGuid(), this.session.Assembler.Cache, this.uri)
                {Value = "mikki.home@mouse.com", VcardType = VcardEmailAddressKind.HOME};

            person.DefaultEmailAddress = email1;

            var email2 = new EmailAddress(Guid.NewGuid(), this.session.Assembler.Cache, this.uri)
                {Value = "mikki.work@mouse.com", VcardType = VcardEmailAddressKind.WORK};

            var modifiedSiteDirectory = this.session.Assembler.RetrieveSiteDirectory().Clone(true);

            var transaction = new ThingTransaction(
                TransactionContextResolver.ResolveContext(modifiedSiteDirectory), modifiedSiteDirectory);
            transaction.Create(person, modifiedSiteDirectory);
            transaction.Create(email1, person);
            transaction.Create(email2, person);

            this.session.Write(transaction.FinalizeTransaction()).GetAwaiter().GetResult();

            this.PrintCacheCount();

            this.PrintCommands();
        }

        /// <summary>
        /// Prints current amount of objects in the cache
        /// </summary>
        private void PrintCacheCount()
        {
            Console.WriteLine(this.session.Assembler.Cache.Count + " objects currently in the cache.");
        }

        /// <summary>
        /// Prints a list of available commands to the console
        /// </summary>
        private void PrintCommands()
        {
            this.PrintSeparator();
            Console.WriteLine("Available commands:");
            Console.WriteLine(OpenConnection + " - Open a connection to a data-source");
            Console.WriteLine(RefreshData + " - Update the Cache with updated Things from a data-source");
            Console.WriteLine(ReloadData + " - Reload all Things from a data-source for all open TopContainers");
            Console.WriteLine(
                CloseConnection
                + " - Close the connection to a data-source and clear the Cache and exits the program");
            Console.WriteLine(RestoreServer + " - Restores the state of a data-source to its default state");
            Console.WriteLine(
                OpenIteration
                + " - gets a predefined iteration of an engineering model with dependent objects");
            Console.WriteLine(PostPredifinedPerson + " - posts a predefined person with 2 e-mail addresses");
            Console.WriteLine(PostPredifinedParameter + " - posts a predefined parameter");
            Console.WriteLine(
                PostPfsl + " - posts a predefined PossibleFiniteStateList with 2 PossibleFiniteStates");
            Console.WriteLine(
                PostPfslReorder
                + " - reorders(rotates in this particular case) states in the created predefined PossibleFiniteStateList (post_pfsl)");
            Console.WriteLine(RemoveParameterFromElementDefinition +
                              " - removes a predefined Parameter of ElementDefinition");
            this.PrintSeparator();
        }

        /// <summary>
        /// Checks whether <see cref="SiteDirectory"/> is unavailable
        /// </summary>
        private bool IsSiteDirectoryUnavailable()
        {
            return this.session.Assembler.RetrieveSiteDirectory() == null;
        }
    }
}