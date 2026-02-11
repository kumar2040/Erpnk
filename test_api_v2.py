import urllib.request
import urllib.parse
import json
import time

API_URL = "http://localhost:5271"

def test():
    # Login
    login_payload = {
        "email": "admin@nkplm.erp",
        "password": "Password123!"
    }
    
    print("Waiting for API...")
    for i in range(30):
        try:
            with urllib.request.urlopen(f"{API_URL}/swagger") as response:
                if response.status == 200:
                    break
        except:
            if i % 5 == 0: print(f"Waiting... {i}")
            time.sleep(1)
            
    print("Logging in...")
    try:
        data = json.dumps(login_payload).encode('utf-8')
        req = urllib.request.Request(f"{API_URL}/api/v1/auth/login", data=data, headers={'Content-Type': 'application/json'})
        with urllib.request.urlopen(req) as response:
            resp_body = response.read().decode('utf-8')
            resp_json = json.loads(resp_body)
            token = resp_json.get("token")
            
        if not token:
             print(f"No token in response: {resp_json}")
             return
             
        print("Logged in. Token obtained.")
    except Exception as e:
        print(f"Login failed: {e}")
        return

    # Call BuyerOrderSummary
    params = urllib.parse.urlencode({"year": 2026, "type": "All"})
    url = f"{API_URL}/api/v1/BuyerOrderSummary?{params}"
    print(f"Calling {url}...")
    
    req = urllib.request.Request(url, headers={"Authorization": f"Bearer {token}"})
    try:
        with urllib.request.urlopen(req) as response:
            print(f"Status: {response.status}")
            print(f"Response: {response.read().decode('utf-8')}")
    except urllib.error.HTTPError as e:
        print(f"HTTP Error: {e.code} {e.read().decode('utf-8')}")
    except Exception as e:
        print(f"Error: {e}")

if __name__ == "__main__":
    test()
