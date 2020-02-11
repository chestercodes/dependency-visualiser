
// view loaded nodes
MATCH (n) RETURN n LIMIT 25

// deployed framework project names that reference standard libraries
MATCH p=(s:Project{deployed: "1", platform: "netframework" })-[r:REFERENCES{platform: 'standard' }]->(e:Library)  
RETURN DISTINCT s.name LIMIT 250

// projects and type of library for a given lib name
MATCH p=(s:Project)-[r:REFERENCES]->(e:Library{name: "Newtonsoft.Json" })  
RETURN s.name, r.version, r.platform LIMIT 250

// projects that can talk to database
MATCH p=(p1)-[r:CAN_TALK_TO]->(d:Resource{ name:'DatabaseName' }) 
RETURN p1.name LIMIT 250

// delete all!
MATCH (n)
DETACH DELETE n
