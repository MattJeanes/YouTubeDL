// Abyss Web Interface

cluster = require('cluster');
if(cluster.isMaster){
	console.log("Master process started")
	cluster.fork()
	cluster.on("exit", function(){
		cluster.fork()
	})
	return
}else{
	console.log("Fork started")
}

var port = 8090
var maxlength=10
var tempfolder="tmp"
var outfolder="out"
var jsonfolder="json"

var fs = require("fs")
var vm = require("vm")
var path = require("path")
var child_process = require("child_process")
var exec = child_process.execFile
var stream = require('stream');
var PassThrough = stream.PassThrough
var util = require('util');
var spawn=child_process.spawn;
var glob = require("glob")
var ffmpeg = require('fluent-ffmpeg')
var StreamCache = require('stream-cache')

var sslfolder = "C:\\Users\\Jarvis\\AppData\\Roaming\\letsencrypt-win-simple\\httpsacme-v01.api.letsencrypt.org"
var pfx  = fs.readFileSync(path.join(sslfolder,'abyss.mattjeanes.com-all.pfx'));
var credentials = {pfx:pfx};

app = require("express")()
http = require("https").Server(credentials,app)

function getoutputfile(id){
	return path.join(__dirname,outfolder,id+".mp3")
}

function getjsonfile(id){
	return path.join(__dirname,jsonfolder,id+".json")
}

var converts={}
function download(id,res,json){
	converts[id]={};
	converts[id].cache=new StreamCache();
	var dl=spawn("youtube-dl.exe",[
		"--youtube-skip-dash-manifest",
		"--extract-audio",
		"--output",
		path.join(tempfolder,"%(id)s.%(ext)s"),
		"--",
		id
	],{cwd:__dirname})
	converts[id].downloadproc=dl
	dl.on('exit', function(){
		glob(path.join(tempfolder,id+"*"),function(err,files){
			if(err){
				json.err="Internal server error"
				fail(id,res,json)
			}else{
				var file=files[0]
				if(!file){
					json.err="Failed to download video, does it exist?"
					fail(id,res,json)
				}else{
					converts[id].download=true
					convert(id,file,res,json)
				}
			}
		})
	})
}

function deleteoriginal(id,file){
	console.log("Deleted original file: "+file)
	fs.exists(file,function(exists){
		if(!exists){
			console.log("Could not find: "+file)
		}else{
			fs.unlink(file, function(err){
				if(err){
					console.log("Failed to delete "+file)
				}else{
					console.log("Successfully deleted "+file);
				}
			})
		}
	})
}

function fail(id,res,json){
	delete converts[id];
	console.log("Convert failed on video ID: "+id)
	res.status(500)
	res.json(json)
	
	var file=getoutputfile(id)
	fs.exists(file,function(exists){
		if(!exists){
			console.log("Could not find: "+file)
		}else{
			fs.unlink(file, function(err){
				if(err){
					console.log("Failed to delete "+file)
				}else{
					console.log("Successfully deleted "+file);
				}
			})
		}
	})
	
}

function convert(id,file,res,json){
	console.log("Converting video ID: "+id)
	var pass=new PassThrough()
	converts[id].pass=pass
	var stream=fs.createWriteStream(getoutputfile(id))
	var cmd=new ffmpeg()
	converts[id].convertproc=cmd;
	cmd.input(file)
	cmd.format('mp3')
	cmd.on('end', function() {
		pass.end()
		delete converts[id]
		deleteoriginal(id,file)
		console.log("Convert finished on video ID: "+id)
	})
	cmd.on('error',function(err){
		console.log(err)
		json.err="Conversion to MP3 failed"
		deleteoriginal(id,file)
		fail(id,res,json)
	})
	
	pass.on('end',function(){
		stream.end()
		converts[id].cache.end()
	})
					
	cmd.output(pass)
	pass.pipe(stream);
	pass.pipe(converts[id].cache);
	cmd.run();
	json.success=true
	res.json(json)
}

function getinprogress(id,res){
	var convert=converts[id]
	if(convert){
		if(convert.cache){
			convert.cache.pipe(res)
		}
	}
}

function abort(id){
	var convert=converts[id]
	if(convert){
		if(!convert.download && convert.downloadproc){
			convert.downloadproc.kill()
		}
		if(convert.convertproc){
			convert.convertproc.kill()
		}
	}
}

function savejson(id,json){
	var filename=getjsonfile(id)
	fs.writeFile(filename, JSON.stringify(json,null,"\t"), function(err){
		if(err) {
			console.log("Failed to save JSON: "+err)
		}else{
			console.log("JSON saved to "+filename);
		}
	})
}

function loadjson(id,callback){
	var filename=getjsonfile(id)
	fs.exists(filename,function(exists){
		if(exists){
			fs.readFile(filename,function(err,data){
				if(err){
					callback("Failed to load JSON file: "+err)
				}else{
					try{
						var o=JSON.parse(data)
						console.log("Loaded JSON from file: "+id)
						callback(null,o)
					}catch(e){
						callback("Failed to load JSON data: "+err)
					}
					
				}
			})
		}else{
			callback("JSON file does not exist")
		}
	})
}

function getjson(id,callback){
	var filename=getjsonfile(id)
	fs.exists(filename,function(exists){
		if(exists){
			loadjson(id,function(err,json){
				callback(err,json)
			})
		}else{
			console.log("Downloading data for video ID: "+id)
			var jsonstr=""
			var dl=spawn("youtube-dl.exe",["--dump-json",
				"--",
				id
			],{cwd:__dirname})
			dl.stdout.on('data',function(data){
				jsonstr+=data.toString()
			})
			dl.on('exit', function(){
				try{
					// Magic JSON fixing
					jsonstr = jsonstr.replace(/\\n/g, "\\n")  
						.replace(/\\'/g, "\\'")
						.replace(/\\"/g, '\\"')
						.replace(/\\&/g, "\\&")
						.replace(/\\r/g, "\\r")
						.replace(/\\t/g, "\\t")
						.replace(/\\b/g, "\\b")
						.replace(/\\f/g, "\\f")
						.replace(/[\u0000-\u0019]+/g,"")
						
					var o=JSON.parse(jsonstr)
					
					savejson(id,o)
					
					callback(null,o)
				}catch(e){
					callback("Failed to load video info - does it exist?")
				}
			})
		}
	})
}

function adddata(o,json){
	console.log("Video title: "+o.fulltitle)
	json.title=o.fulltitle
	json.id=o.id
	json.duration=o.duration
	json.like_count=o.like_count
	json.dislike_count=o.dislike_count
	json.description=o.description
	json.uploader=o.uploader
}

function check(id,json,callback){
	getjson(id,function(err,o){
		if(err){
			callback(err)
		}else{
			adddata(o,json)
			if(o.duration>60*maxlength){
				callback("Cannot convert videos longer than "+maxlength+" minutes")
			}else{
				callback()
			}
		}
	})
}

// Create folders if not exists
fs.existsSync(outfolder) || fs.mkdirSync(outfolder)
fs.existsSync(tempfolder) || fs.mkdirSync(tempfolder)
fs.existsSync(jsonfolder) || fs.mkdirSync(jsonfolder)

app.get("/get", function(req, res){
	var json={}
	var id=req.query.id
	if(id){
		if(converts[id]){
			json.success=true
			res.json(json)
		}else{
			var file=getoutputfile(id);
			fs.exists(file,function(exists){
				if(exists){
					getjson(id,function(err,o){
						if(err){
							json.err=err
						}else{
							adddata(o,json)
							json.success=true
							res.json(json)
						}
					})
				}else{
					console.log("Checking video ID: "+id)
					check(id,json,function(err){
						if(err){
							json.err=err
							res.json(json)
						}else{
							download(id,res,json)
						}
					})
					
				}
			})
		}
	}else{
		json.err="Video ID not supplied"
		res.json(json)
	}
})

app.get("/play", function(req, res){
	res.setHeader('Content-Type', 'audio/mpeg')
	var id=req.query.id
	if(id){
		if(converts[id]){
			console.log("Already converting video ID: "+id)
			getinprogress(id,res)
		}else{
			var file=getoutputfile(id);
			fs.exists(file,function(exists){
				if(exists){
					console.log("Existing audio found for video ID: "+id)
					var stream=fs.createReadStream(file)
					stream.pipe(res)
					stream.on("end",function(){
						res.end()
					})
				}else{
					res.status(500)
					res.end()
				}
			})
		}
	}else{
		res.status(400)
		res.end()
	}
})

http.listen(port,function(err){
	if(err){
		console.log("HTTP failed to open port")
	}else{
		console.log("HTTP listening on " + port)
	}
})