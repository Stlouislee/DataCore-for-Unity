using UnityEngine;
using UnityEngine.Assertions;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

namespace AroAro.DataCore.Tests
{
    /// <summary>
    /// Graph 图数据集测试 - 在运行时生成复杂的图数据结构
    /// Migrated from MonoBehaviour to NUnit for CI/CD compatibility.
    /// Uses DataCoreStore directly instead of scene-dependent DataCoreEditorComponent.
    /// </summary>
    [TestFixture]
    public class GraphDatasetTest
    {
        public enum GraphType
        {
            SocialNetwork,
            OrganizationTree,
            KnowledgeGraph,
            CityNetwork,
            MolecularStructure
        }

        [Test]
        public void RunGraphTest_SocialNetwork()
        {
            RunGraphTest(GraphType.SocialNetwork, "complex-graph-test");
        }

        [Test]
        public void RunGraphTest_OrganizationTree()
        {
            RunGraphTest(GraphType.OrganizationTree, "org-tree-test");
        }

        [Test]
        public void RunGraphTest_KnowledgeGraph()
        {
            RunGraphTest(GraphType.KnowledgeGraph, "knowledge-graph-test");
        }

        [Test]
        public void RunGraphTest_CityNetwork()
        {
            RunGraphTest(GraphType.CityNetwork, "city-network-test");
        }

        [Test]
        public void RunGraphTest_MolecularStructure()
        {
            RunGraphTest(GraphType.MolecularStructure, "molecular-test");
        }

        private void RunGraphTest(GraphType graphType, string datasetName)
        {
            Random.InitState(42);

            Debug.Log($"=== Starting Graph Dataset Test: {graphType} ===");

            using var store = new DataCoreStore();

            if (store.HasDataset(datasetName))
            {
                store.Delete(datasetName);
            }

            var graph = store.CreateGraph(datasetName);

            switch (graphType)
            {
                case GraphType.SocialNetwork:
                    CreateSocialNetworkGraph(graph);
                    break;
                case GraphType.OrganizationTree:
                    CreateOrganizationTree(graph);
                    break;
                case GraphType.KnowledgeGraph:
                    CreateKnowledgeGraph(graph);
                    break;
                case GraphType.CityNetwork:
                    CreateCityNetworkGraph(graph);
                    break;
                case GraphType.MolecularStructure:
                    CreateMolecularStructure(graph);
                    break;
            }

            Debug.Log($"=== Graph Creation Complete ===");
            Debug.Log($"Dataset Name: {graph.Name}");
            Debug.Log($"Total Nodes: {graph.NodeCount}");
            Debug.Log($"Total Edges: {graph.EdgeCount}");
            Debug.Log($"Graph Type: {graphType}");

            PerformGraphQueries(graph);
        }

        #region 图生成方法

        private void CreateSocialNetworkGraph(IGraphDataset graph)
        {
            Debug.Log("Creating Social Network Graph...");

            var userNodes = new List<(string Id, IDictionary<string, object> Properties)>();
            var friendEdges = new List<(string From, string To, IDictionary<string, object> Properties)>();

            for (int i = 1; i <= 50; i++)
            {
                var userId = $"user_{i}";
                var properties = new Dictionary<string, object>
                {
                    ["type"] = "user",
                    ["username"] = $"User{i}",
                    ["age"] = 18 + (i % 50),
                    ["city"] = GetRandomCity(),
                    ["interests"] = GetRandomInterests(),
                    ["followers_count"] = Random.Range(10, 1000),
                    ["posts_count"] = Random.Range(5, 500),
                    ["registration_date"] = $"2023-{Random.Range(1, 13):D2}-{Random.Range(1, 29):D2}",
                    ["is_verified"] = Random.value > 0.7
                };
                userNodes.Add((userId, properties));
            }

            graph.AddNodes(userNodes);
            Debug.Log($"Added {userNodes.Count} user nodes");

            for (int i = 1; i <= 50; i++)
            {
                var fromUser = $"user_{i}";
                var followCount = Random.Range(3, 15);

                for (int j = 0; j < followCount; j++)
                {
                    var toUserId = Random.Range(1, 51);
                    if (toUserId != i)
                    {
                        var toUser = $"user_{toUserId}";

                        if (!graph.HasEdge(fromUser, toUser))
                        {
                            var edgeProps = new Dictionary<string, object>
                            {
                                ["relationship"] = "follows",
                                ["since"] = $"2023-{Random.Range(1, 13):D2}-{Random.Range(1, 29):D2}",
                                ["interaction_score"] = Random.Range(0.1f, 1.0f),
                                ["message_count"] = Random.Range(0, 50)
                            };
                            friendEdges.Add((fromUser, toUser, edgeProps));
                        }
                    }
                }
            }

            graph.AddEdges(friendEdges);
            Debug.Log($"Added {friendEdges.Count} follow relationships");

            CreateSocialCommunities(graph);
        }

        private void CreateSocialCommunities(IGraphDataset graph)
        {
            var communities = new[] { "Tech", "Gaming", "Sports", "Music", "Art", "Science", "Travel", "Food" };
            var communityEdges = new List<(string From, string To, IDictionary<string, object> Properties)>();

            foreach (var community in communities)
            {
                var communityId = $"community_{community.ToLower()}";
                graph.AddNode(communityId, new Dictionary<string, object>
                {
                    ["type"] = "community",
                    ["name"] = community,
                    ["member_count"] = Random.Range(100, 10000),
                    ["created_date"] = "2022-01-01",
                    ["description"] = $"A community for {community} enthusiasts"
                });

                for (int i = 1; i <= 50; i++)
                {
                    if (Random.value > 0.5f)
                    {
                        communityEdges.Add(($"user_{i}", communityId, new Dictionary<string, object>
                        {
                            ["relationship"] = "member_of",
                            ["join_date"] = $"2023-{Random.Range(1, 13):D2}-{Random.Range(1, 29):D2}",
                            ["activity_level"] = Random.Range(1, 10)
                        }));
                    }
                }
            }

            graph.AddEdges(communityEdges);
            Debug.Log($"Added {communities.Length} communities and {communityEdges.Count} memberships");
        }

        private void CreateOrganizationTree(IGraphDataset graph)
        {
            Debug.Log("Creating Organization Tree...");

            graph.AddNode("ceo", new Dictionary<string, object>
            {
                ["type"] = "executive",
                ["name"] = "Alice Johnson",
                ["title"] = "CEO",
                ["level"] = 1,
                ["department"] = "Executive",
                ["salary_grade"] = "E1",
                ["years_in_company"] = 15
            });

            var vpDepartments = new[] { "Engineering", "Sales", "Marketing", "Finance", "HR", "Operations" };
            foreach (var dept in vpDepartments)
            {
                var vpId = $"vp_{dept.ToLower()}";
                graph.AddNode(vpId, new Dictionary<string, object>
                {
                    ["type"] = "executive",
                    ["name"] = $"VP of {dept}",
                    ["title"] = "Vice President",
                    ["level"] = 2,
                    ["department"] = dept,
                    ["salary_grade"] = "E2",
                    ["team_size"] = Random.Range(20, 100),
                    ["years_in_company"] = Random.Range(5, 12)
                });

                graph.AddEdge("ceo", vpId, new Dictionary<string, object>
                {
                    ["relationship"] = "manages",
                    ["direct_report"] = true
                });

                int managerCount = Random.Range(3, 6);
                for (int i = 1; i <= managerCount; i++)
                {
                    var managerId = $"manager_{dept.ToLower()}_{i}";
                    graph.AddNode(managerId, new Dictionary<string, object>
                    {
                        ["type"] = "manager",
                        ["name"] = $"{dept} Manager {i}",
                        ["title"] = "Manager",
                        ["level"] = 3,
                        ["department"] = dept,
                        ["salary_grade"] = $"M{Random.Range(1, 4)}",
                        ["team_size"] = Random.Range(5, 15),
                        ["years_in_company"] = Random.Range(2, 8)
                    });

                    graph.AddEdge(vpId, managerId, new Dictionary<string, object>
                    {
                        ["relationship"] = "manages",
                        ["direct_report"] = true
                    });

                    int employeeCount = Random.Range(3, 8);
                    for (int j = 1; j <= employeeCount; j++)
                    {
                        var empId = $"emp_{dept.ToLower()}_{i}_{j}";
                        graph.AddNode(empId, new Dictionary<string, object>
                        {
                            ["type"] = "employee",
                            ["name"] = $"{dept} Employee {i}-{j}",
                            ["title"] = GetRandomJobTitle(dept),
                            ["level"] = 4,
                            ["department"] = dept,
                            ["salary_grade"] = $"IC{Random.Range(1, 5)}",
                            ["years_in_company"] = Random.Range(0, 5),
                            ["skills"] = GetRandomSkills(dept)
                        });

                        graph.AddEdge(managerId, empId, new Dictionary<string, object>
                        {
                            ["relationship"] = "manages",
                            ["direct_report"] = true,
                            ["performance_rating"] = Random.Range(3.0f, 5.0f)
                        });
                    }
                }
            }

            AddCrossDepartmentRelations(graph);
        }

        private void AddCrossDepartmentRelations(IGraphDataset graph)
        {
            var nodeIds = graph.GetNodeIds().ToList();
            var employeeNodes = nodeIds.Where(id => id.StartsWith("emp_")).ToList();

            for (int i = 0; i < 30; i++)
            {
                if (employeeNodes.Count > 1)
                {
                    var emp1 = employeeNodes[Random.Range(0, employeeNodes.Count)];
                    var emp2 = employeeNodes[Random.Range(0, employeeNodes.Count)];

                    if (emp1 != emp2 && !graph.HasEdge(emp1, emp2))
                    {
                        graph.AddEdge(emp1, emp2, new Dictionary<string, object>
                        {
                            ["relationship"] = "collaborates_with",
                            ["project"] = $"Project-{Random.Range(100, 999)}",
                            ["frequency"] = Random.Range(1, 10)
                        });
                    }
                }
            }

            Debug.Log("Added cross-department collaboration relationships");
        }

        private void CreateKnowledgeGraph(IGraphDataset graph)
        {
            Debug.Log("Creating Knowledge Graph...");

            var subjects = new Dictionary<string, string[]>
            {
                ["Mathematics"] = new[] { "Algebra", "Calculus", "Geometry", "Statistics", "NumberTheory" },
                ["Physics"] = new[] { "Mechanics", "Thermodynamics", "Quantum", "Relativity", "Electromagnetism" },
                ["ComputerScience"] = new[] { "Algorithms", "DataStructures", "AI", "MachineLearning", "Networks" },
                ["Biology"] = new[] { "Genetics", "Evolution", "Ecology", "Molecular", "Anatomy" },
                ["Chemistry"] = new[] { "Organic", "Inorganic", "Physical", "Analytical", "Biochemistry" }
            };

            var allNodes = new List<(string Id, IDictionary<string, object> Properties)>();
            var allEdges = new List<(string From, string To, IDictionary<string, object> Properties)>();

            foreach (var subject in subjects.Keys)
            {
                allNodes.Add(($"subject_{subject}", new Dictionary<string, object>
                {
                    ["type"] = "subject",
                    ["name"] = subject,
                    ["level"] = "domain",
                    ["field_size"] = Random.Range(1000, 100000)
                }));
            }

            foreach (var kvp in subjects)
            {
                var parentId = $"subject_{kvp.Key}";

                foreach (var subfield in kvp.Value)
                {
                    var subfieldId = $"subfield_{subfield}";
                    allNodes.Add((subfieldId, new Dictionary<string, object>
                    {
                        ["type"] = "subfield",
                        ["name"] = subfield,
                        ["parent_subject"] = kvp.Key,
                        ["level"] = "subfield"
                    }));

                    allEdges.Add((parentId, subfieldId, new Dictionary<string, object>
                    {
                        ["relationship"] = "contains"
                    }));
                }
            }

            var allSubfields = subjects.Values.SelectMany(x => x).ToArray();
            for (int i = 1; i <= 100; i++)
            {
                var subfield = allSubfields[Random.Range(0, allSubfields.Length)];
                var conceptId = $"concept_{i}";

                allNodes.Add((conceptId, new Dictionary<string, object>
                {
                    ["type"] = "concept",
                    ["name"] = $"Concept {i}",
                    ["subfield"] = subfield,
                    ["complexity"] = Random.Range(1, 10),
                    ["year_introduced"] = Random.Range(1900, 2024),
                    ["citations"] = Random.Range(10, 10000)
                }));

                allEdges.Add(($"subfield_{subfield}", conceptId, new Dictionary<string, object>
                {
                    ["relationship"] = "contains_concept"
                }));
            }

            Debug.Log($"Adding {allNodes.Count} nodes...");
            graph.AddNodes(allNodes);

            Debug.Log($"Adding {allEdges.Count} structural edges...");
            graph.AddEdges(allEdges);

            var conceptEdges = new List<(string From, string To, IDictionary<string, object> Properties)>();
            for (int i = 1; i <= 100; i++)
            {
                var relCount = Random.Range(1, 5);
                for (int j = 0; j < relCount; j++)
                {
                    var targetId = Random.Range(1, 101);
                    if (targetId != i)
                    {
                        var from = $"concept_{i}";
                        var to = $"concept_{targetId}";

                        if (!conceptEdges.Any(e => e.From == from && e.To == to))
                        {
                            var relType = GetRandomRelationType();
                            conceptEdges.Add((from, to, new Dictionary<string, object>
                            {
                                ["relationship"] = relType,
                                ["strength"] = Random.Range(0.1f, 1.0f)
                            }));
                        }
                    }
                }
            }

            if (conceptEdges.Count > 0)
            {
                Debug.Log($"Adding {conceptEdges.Count} concept relationships...");
                graph.AddEdges(conceptEdges);
            }

            AddScientistNodes(graph, allSubfields);
        }

        private void AddScientistNodes(IGraphDataset graph, string[] subfields)
        {
            var scientists = new[]
            {
                ("Einstein", "Physics", 1955),
                ("Newton", "Physics", 1727),
                ("Darwin", "Biology", 1882),
                ("Curie", "Chemistry", 1934),
                ("Turing", "ComputerScience", 1954),
                ("Euler", "Mathematics", 1783),
                ("Feynman", "Physics", 1988),
                ("Mendel", "Biology", 1884),
                ("Pasteur", "Biology", 1895),
                ("Gauss", "Mathematics", 1855)
            };

            var scientistEdges = new List<(string From, string To, IDictionary<string, object> Properties)>();

            foreach (var (name, field, year) in scientists)
            {
                var scientistId = $"scientist_{name}";
                graph.AddNode(scientistId, new Dictionary<string, object>
                {
                    ["type"] = "scientist",
                    ["name"] = name,
                    ["field"] = field,
                    ["death_year"] = year,
                    ["notable_works"] = Random.Range(5, 50),
                    ["awards"] = Random.Range(1, 10)
                });

                scientistEdges.Add((scientistId, $"subject_{field}", new Dictionary<string, object>
                {
                    ["relationship"] = "contributed_to"
                }));

                var conceptCount = Random.Range(3, 8);
                for (int i = 0; i < conceptCount; i++)
                {
                    var conceptId = $"concept_{Random.Range(1, 101)}";
                    if (graph.HasNode(conceptId))
                    {
                        scientistEdges.Add((scientistId, conceptId, new Dictionary<string, object>
                        {
                            ["relationship"] = "discovered",
                            ["year"] = Random.Range(year - 50, year)
                        }));
                    }
                }
            }

            if (scientistEdges.Count > 0)
            {
                graph.AddEdges(scientistEdges);
                Debug.Log($"Added {scientists.Length} scientists and {scientistEdges.Count} relationships");
            }
        }

        private void CreateCityNetworkGraph(IGraphDataset graph)
        {
            Debug.Log("Creating City Network Graph...");

            var cityNames = new[]
            {
                "Beijing", "Shanghai", "Guangzhou", "Shenzhen", "Chengdu",
                "Hangzhou", "Wuhan", "Xi'an", "Nanjing", "Chongqing",
                "Tianjin", "Suzhou", "Changsha", "Zhengzhou", "Kunming",
                "Tokyo", "Seoul", "Singapore", "Bangkok", "Mumbai",
                "Dubai", "Istanbul", "Moscow", "London", "Paris",
                "NewYork", "LosAngeles", "Chicago", "Toronto", "Sydney"
            };

            var cityNodes = new List<(string Id, IDictionary<string, object> Properties)>();

            for (int i = 0; i < cityNames.Length; i++)
            {
                var cityId = $"city_{cityNames[i]}";
                cityNodes.Add((cityId, new Dictionary<string, object>
                {
                    ["type"] = "city",
                    ["name"] = cityNames[i],
                    ["population"] = Random.Range(1000000, 30000000),
                    ["area_km2"] = Random.Range(500, 20000),
                    ["gdp_billion"] = Random.Range(100, 3000),
                    ["latitude"] = Random.Range(-90f, 90f),
                    ["longitude"] = Random.Range(-180f, 180f),
                    ["timezone"] = $"UTC+{Random.Range(-12, 13)}"
                }));
            }

            graph.AddNodes(cityNodes);

            var transportEdges = new List<(string From, string To, IDictionary<string, object> Properties)>();

            for (int i = 0; i < cityNames.Length; i++)
            {
                var fromCity = $"city_{cityNames[i]}";
                var connectionCount = Random.Range(3, 8);

                for (int j = 0; j < connectionCount; j++)
                {
                    var toIndex = Random.Range(0, cityNames.Length);
                    if (toIndex != i)
                    {
                        var toCity = $"city_{cityNames[toIndex]}";

                        if (!graph.HasEdge(fromCity, toCity))
                        {
                            var distance = Random.Range(100, 15000);
                            var transportType = GetRandomTransportType();

                            transportEdges.Add((fromCity, toCity, new Dictionary<string, object>
                            {
                                ["transport_type"] = transportType,
                                ["distance_km"] = distance,
                                ["travel_time_hours"] = CalculateTravelTime(distance, transportType),
                                ["cost_usd"] = Random.Range(50, 2000),
                                ["daily_frequency"] = Random.Range(1, 50),
                                ["is_direct"] = Random.value > 0.3
                            }));
                        }
                    }
                }
            }

            graph.AddEdges(transportEdges);

            AddTransportHubs(graph, cityNames);
        }

        private void AddTransportHubs(IGraphDataset graph, string[] cityNames)
        {
            foreach (var cityName in cityNames.Take(15))
            {
                var airportId = $"airport_{cityName}";
                graph.AddNode(airportId, new Dictionary<string, object>
                {
                    ["type"] = "airport",
                    ["name"] = $"{cityName} International Airport",
                    ["code"] = GenerateAirportCode(cityName),
                    ["capacity_million"] = Random.Range(10, 100),
                    ["terminals"] = Random.Range(2, 8)
                });

                graph.AddEdge($"city_{cityName}", airportId, new Dictionary<string, object>
                {
                    ["relationship"] = "has_airport"
                });

                var stationId = $"station_{cityName}";
                graph.AddNode(stationId, new Dictionary<string, object>
                {
                    ["type"] = "train_station",
                    ["name"] = $"{cityName} Central Station",
                    ["platforms"] = Random.Range(5, 30),
                    ["daily_passengers"] = Random.Range(10000, 500000)
                });

                graph.AddEdge($"city_{cityName}", stationId, new Dictionary<string, object>
                {
                    ["relationship"] = "has_station"
                });
            }
        }

        private void CreateMolecularStructure(IGraphDataset graph)
        {
            Debug.Log("Creating Molecular Structure Graph...");

            CreateCaffeineaMolecule(graph);
            CreateAspirinMolecule(graph);
            CreateGlucoseMolecule(graph);
            CreateEthanolMolecule(graph);
            CreateDNAFragment(graph);
        }

        private void CreateCaffeineaMolecule(IGraphDataset graph)
        {
            var prefix = "caffeine";
            var atoms = new List<(string Id, string Element, int Index)>
            {
                ($"{prefix}_C1", "C", 1), ($"{prefix}_C2", "C", 2), ($"{prefix}_C3", "C", 3),
                ($"{prefix}_C4", "C", 4), ($"{prefix}_C5", "C", 5), ($"{prefix}_C6", "C", 6),
                ($"{prefix}_C7", "C", 7), ($"{prefix}_C8", "C", 8),
                ($"{prefix}_N1", "N", 1), ($"{prefix}_N2", "N", 2), ($"{prefix}_N3", "N", 3), ($"{prefix}_N4", "N", 4),
                ($"{prefix}_O1", "O", 1), ($"{prefix}_O2", "O", 2)
            };

            foreach (var (id, element, index) in atoms)
            {
                graph.AddNode(id, new Dictionary<string, object>
                {
                    ["type"] = "atom",
                    ["element"] = element,
                    ["molecule"] = "Caffeine",
                    ["index"] = index,
                    ["atomic_number"] = GetAtomicNumber(element),
                    ["mass"] = GetAtomicMass(element)
                });
            }

            var bonds = new List<(string From, string To, string BondType)>
            {
                ($"{prefix}_C1", $"{prefix}_C2", "single"),
                ($"{prefix}_C2", $"{prefix}_C3", "double"),
                ($"{prefix}_C3", $"{prefix}_C4", "single"),
                ($"{prefix}_C4", $"{prefix}_N1", "single"),
                ($"{prefix}_N1", $"{prefix}_C5", "single"),
                ($"{prefix}_C5", $"{prefix}_N2", "double"),
                ($"{prefix}_N2", $"{prefix}_C6", "single"),
                ($"{prefix}_C6", $"{prefix}_O1", "double"),
                ($"{prefix}_C7", $"{prefix}_N3", "single"),
                ($"{prefix}_C8", $"{prefix}_N4", "single"),
                ($"{prefix}_C1", $"{prefix}_O2", "double")
            };

            foreach (var (from, to, bondType) in bonds)
            {
                graph.AddEdge(from, to, new Dictionary<string, object>
                {
                    ["bond_type"] = bondType,
                    ["bond_length"] = GetBondLength(bondType),
                    ["bond_energy"] = Random.Range(200, 800)
                });
            }

            Debug.Log("Added Caffeine molecule structure");
        }

        private void CreateAspirinMolecule(IGraphDataset graph)
        {
            var prefix = "aspirin";

            for (int i = 1; i <= 9; i++)
            {
                graph.AddNode($"{prefix}_C{i}", new Dictionary<string, object>
                {
                    ["type"] = "atom", ["element"] = "C", ["molecule"] = "Aspirin", ["index"] = i, ["atomic_number"] = 6
                });
            }

            for (int i = 1; i <= 4; i++)
            {
                graph.AddNode($"{prefix}_O{i}", new Dictionary<string, object>
                {
                    ["type"] = "atom", ["element"] = "O", ["molecule"] = "Aspirin", ["index"] = i, ["atomic_number"] = 8
                });
            }

            graph.AddEdge($"{prefix}_C1", $"{prefix}_C2", new Dictionary<string, object> { ["bond_type"] = "double" });
            graph.AddEdge($"{prefix}_C2", $"{prefix}_C3", new Dictionary<string, object> { ["bond_type"] = "single" });
            graph.AddEdge($"{prefix}_C1", $"{prefix}_O1", new Dictionary<string, object> { ["bond_type"] = "single" });

            Debug.Log("Added Aspirin molecule structure");
        }

        private void CreateGlucoseMolecule(IGraphDataset graph)
        {
            var prefix = "glucose";

            for (int i = 1; i <= 6; i++)
            {
                graph.AddNode($"{prefix}_C{i}", new Dictionary<string, object>
                {
                    ["type"] = "atom", ["element"] = "C", ["molecule"] = "Glucose", ["index"] = i, ["in_ring"] = i <= 5
                });
            }

            for (int i = 1; i <= 6; i++)
            {
                graph.AddNode($"{prefix}_O{i}", new Dictionary<string, object>
                {
                    ["type"] = "atom", ["element"] = "O", ["molecule"] = "Glucose", ["index"] = i
                });
            }

            for (int i = 1; i <= 5; i++)
            {
                var next = (i % 5) + 1;
                graph.AddEdge($"{prefix}_C{i}", $"{prefix}_C{next}", new Dictionary<string, object>
                {
                    ["bond_type"] = "single", ["in_ring"] = true
                });
            }

            Debug.Log("Added Glucose molecule structure");
        }

        private void CreateEthanolMolecule(IGraphDataset graph)
        {
            var prefix = "ethanol";

            graph.AddNode($"{prefix}_C1", new Dictionary<string, object> { ["type"] = "atom", ["element"] = "C", ["molecule"] = "Ethanol" });
            graph.AddNode($"{prefix}_C2", new Dictionary<string, object> { ["type"] = "atom", ["element"] = "C", ["molecule"] = "Ethanol" });
            graph.AddNode($"{prefix}_O1", new Dictionary<string, object> { ["type"] = "atom", ["element"] = "O", ["molecule"] = "Ethanol" });

            graph.AddEdge($"{prefix}_C1", $"{prefix}_C2", new Dictionary<string, object> { ["bond_type"] = "single" });
            graph.AddEdge($"{prefix}_C2", $"{prefix}_O1", new Dictionary<string, object> { ["bond_type"] = "single" });

            Debug.Log("Added Ethanol molecule structure");
        }

        private void CreateDNAFragment(IGraphDataset graph)
        {
            var bases = new[] { "A", "T", "G", "C" };
            var sequence = new List<string>();

            for (int i = 0; i < 20; i++)
            {
                sequence.Add(bases[Random.Range(0, bases.Length)]);
            }

            for (int i = 0; i < sequence.Count; i++)
            {
                var baseId = $"dna_base_{i}";
                graph.AddNode(baseId, new Dictionary<string, object>
                {
                    ["type"] = "nucleotide",
                    ["base"] = sequence[i],
                    ["position"] = i,
                    ["strand"] = "forward",
                    ["molecule"] = "DNA"
                });

                if (i > 0)
                {
                    graph.AddEdge($"dna_base_{i - 1}", baseId, new Dictionary<string, object>
                    {
                        ["bond_type"] = "phosphodiester",
                        ["backbone"] = true
                    });
                }

                var complementId = $"dna_complement_{i}";
                var complement = GetComplementBase(sequence[i]);

                graph.AddNode(complementId, new Dictionary<string, object>
                {
                    ["type"] = "nucleotide",
                    ["base"] = complement,
                    ["position"] = i,
                    ["strand"] = "reverse",
                    ["molecule"] = "DNA"
                });

                graph.AddEdge(baseId, complementId, new Dictionary<string, object>
                {
                    ["bond_type"] = "hydrogen",
                    ["bonds_count"] = (sequence[i] == "A" || sequence[i] == "T") ? 2 : 3
                });
            }

            Debug.Log($"Added DNA fragment with {sequence.Count} base pairs");
        }

        #endregion

        #region 查询和分析方法

        private void PerformGraphQueries(IGraphDataset graph)
        {
            Debug.Log("\n=== Performing Graph Queries ===");

            var nodeIds = graph.GetNodeIds().ToList();
            Assert.IsTrue(graph.NodeCount > 0, "Graph should have nodes");
            Assert.IsTrue(graph.EdgeCount > 0, "Graph should have edges");

            if (nodeIds.Count > 0)
            {
                var topNodes = nodeIds
                    .OrderByDescending(id => graph.GetOutDegree(id))
                    .Take(5)
                    .ToList();

                Debug.Log($"Top 5 nodes by out-degree:");
                foreach (var nodeId in topNodes)
                {
                    var outDegree = graph.GetOutDegree(nodeId);
                    var inDegree = graph.GetInDegree(nodeId);
                    var props = graph.GetNodeProperties(nodeId);
                    var nodeName = props.ContainsKey("name") ? props["name"] : nodeId;

                    Debug.Log($"  - {nodeName}: Out={outDegree}, In={inDegree}");
                    Assert.IsTrue(outDegree >= 0, $"Out-degree of {nodeId} should be non-negative");
                }
            }

            if (nodeIds.Count > 0)
            {
                var randomNode = nodeIds[0];
                var neighbors = graph.GetNeighbors(randomNode).ToList();
                var props = graph.GetNodeProperties(randomNode);
                Assert.IsNotNull(props, $"Properties of {randomNode} should not be null");
            }
        }

        #endregion

        #region 辅助方法

        private string GetRandomCity()
        {
            var cities = new[] { "Beijing", "Shanghai", "Shenzhen", "Hangzhou", "Chengdu", "Tokyo", "Seoul", "NewYork", "London", "Paris" };
            return cities[Random.Range(0, cities.Length)];
        }

        private string GetRandomInterests()
        {
            var interests = new[] { "Tech", "Gaming", "Sports", "Music", "Art", "Travel", "Food", "Reading", "Movies", "Photography" };
            var count = Random.Range(1, 4);
            return string.Join(",", interests.OrderBy(x => Random.value).Take(count));
        }

        private string GetRandomJobTitle(string dept)
        {
            var titles = new Dictionary<string, string[]>
            {
                ["Engineering"] = new[] { "Software Engineer", "DevOps Engineer", "QA Engineer", "Data Engineer" },
                ["Sales"] = new[] { "Sales Representative", "Account Manager", "Sales Engineer" },
                ["Marketing"] = new[] { "Marketing Specialist", "Content Creator", "SEO Specialist" },
                ["Finance"] = new[] { "Financial Analyst", "Accountant", "Tax Specialist" },
                ["HR"] = new[] { "HR Specialist", "Recruiter", "Training Coordinator" },
                ["Operations"] = new[] { "Operations Analyst", "Supply Chain Specialist", "Logistics Coordinator" }
            };

            if (titles.ContainsKey(dept))
            {
                var deptTitles = titles[dept];
                return deptTitles[Random.Range(0, deptTitles.Length)];
            }
            return "Specialist";
        }

        private string GetRandomSkills(string dept)
        {
            var skills = new Dictionary<string, string[]>
            {
                ["Engineering"] = new[] { "Python", "Java", "C#", "AWS", "Docker", "Kubernetes" },
                ["Sales"] = new[] { "Salesforce", "Negotiation", "Communication", "CRM" },
                ["Marketing"] = new[] { "SEO", "SEM", "Social Media", "Content Creation", "Analytics" },
                ["Finance"] = new[] { "Excel", "SAP", "Financial Modeling", "Budgeting" },
                ["HR"] = new[] { "Recruitment", "Training", "Performance Management", "HRIS" },
                ["Operations"] = new[] { "Supply Chain", "Logistics", "Process Optimization", "Lean Six Sigma" }
            };

            if (skills.ContainsKey(dept))
            {
                var deptSkills = skills[dept];
                var count = Random.Range(2, 5);
                return string.Join(",", deptSkills.OrderBy(x => Random.value).Take(count));
            }
            return "Management";
        }

        private string GetRandomRelationType()
        {
            var types = new[] { "depends_on", "relates_to", "derives_from", "implies", "contradicts", "supports" };
            return types[Random.Range(0, types.Length)];
        }

        private string GetRandomTransportType()
        {
            var types = new[] { "flight", "high_speed_rail", "train", "bus", "ferry" };
            return types[Random.Range(0, types.Length)];
        }

        private float CalculateTravelTime(int distanceKm, string transportType)
        {
            return transportType switch
            {
                "flight" => distanceKm / 800f,
                "high_speed_rail" => distanceKm / 300f,
                "train" => distanceKm / 100f,
                "bus" => distanceKm / 80f,
                "ferry" => distanceKm / 40f,
                _ => distanceKm / 100f
            };
        }

        private string GenerateAirportCode(string cityName)
        {
            if (cityName.Length >= 3)
                return cityName.Substring(0, 3).ToUpper();
            return cityName.ToUpper().PadRight(3, 'X');
        }

        private int GetAtomicNumber(string element)
        {
            return element switch
            {
                "H" => 1, "C" => 6, "N" => 7, "O" => 8, "S" => 16, _ => 0
            };
        }

        private float GetAtomicMass(string element)
        {
            return element switch
            {
                "H" => 1.008f, "C" => 12.011f, "N" => 14.007f, "O" => 15.999f, "S" => 32.065f, _ => 0f
            };
        }

        private float GetBondLength(string bondType)
        {
            return bondType switch
            {
                "single" => 1.54f, "double" => 1.34f, "triple" => 1.20f, _ => 1.5f
            };
        }

        private string GetComplementBase(string base_)
        {
            return base_ switch
            {
                "A" => "T", "T" => "A", "G" => "C", "C" => "G", _ => "N"
            };
        }

        #endregion
    }
}
