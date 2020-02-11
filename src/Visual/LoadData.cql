// load in nodes and relationships
// quickest way is to 
//      docker exec -it <id> bash
//      bin/cypher-shell
// copy and paste the LOAD CSV queries below.

LOAD CSV WITH HEADERS FROM 'file:///projects.csv' AS line 
CREATE (:Project { name: line.name, deployed: line.deployed, platform: line.platform });

LOAD CSV WITH HEADERS FROM 'file:///libraries.csv' AS line 
CREATE (:Library {  name: line.name});

LOAD CSV WITH HEADERS FROM 'file:///resources.csv' AS line 
CREATE (:Resource { name: line.name});

LOAD CSV WITH HEADERS FROM "file:///project_ref_project.csv" AS csvLine
MATCH (s:Project {name: csvLine.start}),(e:Project {name: csvLine.end})
CREATE (s)-[:REFERENCES]->(e);

LOAD CSV WITH HEADERS FROM "file:///project_ref_runtimelib.csv" AS csvLine
MATCH (s:Project {name: csvLine.project}),(e:Library {name: csvLine.library})
CREATE (s)-[:REFERENCES]->(e);

LOAD CSV WITH HEADERS FROM "file:///project_ref_nugetlib.csv" AS csvLine
MATCH (s:Project {name: csvLine.project}),(e:Library {name: csvLine.library})
CREATE (s)-[:REFERENCES{ version: csvLine.version, platform: csvLine.build }]->(e);

LOAD CSV WITH HEADERS FROM "file:///project_talk_project.csv" AS csvLine
MATCH (s:Project {name: csvLine.start}),(e:Project {name: csvLine.end})
CREATE (s)-[:CAN_TALK_TO]->(e);

LOAD CSV WITH HEADERS FROM "file:///project_talk_resource.csv" AS csvLine
MATCH (s:Project {name: csvLine.project}),(e:Resource {name: csvLine.resource})
CREATE (s)-[:CAN_TALK_TO]->(e);
