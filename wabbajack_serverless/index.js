const { Router } = require('itty-router');
const { gunzip, gunzipSync } = require("zlib");

const withAuthor = async request => {
  const key = request.headers.get("x-api-key");
  if (key != undefined) {
    request.authorKey = key;
    const author = await AUTHOR_KEYS.get(key);
    if (key != undefined) {
      request.author = author;
    }
  }
}

const requireAuthor = async request => {
  if (!request.author) {
    return new Response("Not Authenticated", {status: 401});
  }  
}

const router = Router();

router.get('/heartbeat', ({ params}) => new Response("Alive!"))
router.get('/users', withAuthor, requireAuthor, async ({ params }) => {
  const {keys} = await AUTHOR_KEYS.list();
  return new Response("Author Count: " + JSON.stringify(keys.length));
});

router.post('/users/import', withAuthor, requireAuthor, async request => {
  const body = await request.text();
  var loaded = [];
  for (const line of body.split('\n'))
  {
    var [key, author] = line.split('\t');
    key = key.trim();
    author = author.trim();
    if (!(await AUTHOR_KEYS.get(key))) {
      await AUTHOR_KEYS.put(key, author);
      loaded.push(author);
    }
    
  }
  return new Response(JSON.stringify(loaded));
});

router.post('/authored_files/import', withAuthor, requireAuthor, async request => {
  const filesOnServer = await filesCommandJson("List", "wabbajack");
  var paths = [];
  for (const file of filesOnServer)
  {
    if (file.endsWith("definition.json.gz")) {
      paths.push(file)
    }
  }
  
  var added = [];
  var existing = [];
  for (const key of (await AUTHOR_FILES.list()).keys)
  {
    console.log("Key: " + key.name)
    var [author, name] = key.name.split('\t');
    existing.push(name);
  }
  console.log(existing);

  
  for (const path of paths) {
    console.log(path)
    var found = false;
    for (const key of existing)
    {
      if (path.includes(key)) {
        found = true;
        break;
      }
    }
    if (found) {
      console.log("Already Imported: " + path);
      continue;
    }
    
    const definitionResponse = await filesCommand("Read", path);
    
    if (added.length == 40)
      break;

    try {
      var ab = await definitionResponse.arrayBuffer();
      var buff = Buffer.from(ab);
      console.log("LEN" + ab.byteLength)
      console.log(buff.length)
      const unzipped = gunzipSync(buff);
      const jsonString = new TextDecoder().decode(unzipped);
      console.log(jsonString);
      const definitionJson = JSON.parse(jsonString);
      const keyName = definitionJson.Author + "\t" + definitionJson.MungedName;
      if (await AUTHOR_FILES.get(keyName))
        continue;

      await AUTHOR_FILES.put(keyName, jsonString);
      added.push(keyName)
    }
    catch (e)
    {
      return new Response(e);
    }
    
  }
  return new Response(JSON.stringify(added));
});

router.all('*', () => new Response("Not Found.", { status: 404}));

addEventListener('fetch', event => {
  event.respondWith(router.handle(event.request))
})

async function filesCommand(method, path)
{
  const init = {
    method: 'POST',
    headers: {
      'apikey' : STORAGE_KEY
    }
  }
  console.log({command: "filesCommand", method: method, path: encodeURIComponent(path), init: init});
  const result = await fetch("https://files.wabbajack.org/files?method=" + method + "&path=" + encodeURIComponent(path), init);
  if (!result.ok) {
    console.log(result);
    throw result;
  }
  return result;
}

async function filesCommandJson(method, path)
{
  const result = await filesCommand(method, path);
  return await result.json();
}

/**
 * Respond with hello worker text
 * @param {Request} request
 */
async function handleRequest(request) {
  return new Response('Hello worker!', {
    headers: { 'content-type': 'text/plain' },
  })
}
